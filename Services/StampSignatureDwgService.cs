// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using BotPlusAutocadTool.Models;
using Newtonsoft.Json;
using AcadColor = Autodesk.AutoCAD.Colors.Color;

namespace BotPlusAutocadTool.Services
{
    public static class StampSignatureDwgService
    {
        private const string StoreKey = "BotPlusAutocadTool.StampSignatures";
        private const string MarkerKey = "BotPlusAutocadTool.SignatureSlot";

        private sealed class StoreCache
        {
            public string Hash;
            public Dictionary<string, StampSignature> Signatures;
        }
        // Parsed signature stores per open drawing; re-parsed only when the stored bytes change.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Database, StoreCache> StoreCaches = new();
        // PNG hash -> pixel size of the generated geometry, so existing signature blocks need no image decoding.
        private static readonly Dictionary<string, (int Width, int Height)> GeometrySizes = new(StringComparer.Ordinal);

        public static Dictionary<string, StampSignature> Load(Database database)
        {
            var bytes = ReadStore(database);
            return bytes == null ? new Dictionary<string, StampSignature>() : Parse(bytes);
        }

        /// <summary>Read-only view for synchronization: the parsed store is reused while its bytes are unchanged.</summary>
        private static Dictionary<string, StampSignature> LoadCached(Database database)
        {
            var bytes = ReadStore(database);
            if (bytes == null) { StoreCaches.Remove(database); return new Dictionary<string, StampSignature>(); }
            string hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (StoreCaches.TryGetValue(database, out var cached) && cached.Hash == hash) return cached.Signatures;
            var signatures = Parse(bytes);
            StoreCaches.AddOrUpdate(database, new StoreCache { Hash = hash, Signatures = signatures });
            return signatures;
        }

        private static byte[] ReadStore(Database database)
        {
            using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                var dictionary = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (!dictionary.Contains(StoreKey)) return null;
                var record = (Xrecord)transaction.GetObject(dictionary.GetAt(StoreKey), OpenMode.ForRead);
                using (var data = record.Data)
                using (var stream = new MemoryStream())
                {
                    if (data != null)
                        foreach (var value in data.AsArray())
                            if (value.Value is byte[] chunk) stream.Write(chunk, 0, chunk.Length);
                    if (stream.Length > 16 * 1024 * 1024) throw new InvalidDataException("Хранилище подписей превышает допустимый размер.");
                    return stream.ToArray();
                }
            }
        }

        private static Dictionary<string, StampSignature> Parse(byte[] bytes)
            => JsonConvert.DeserializeObject<Dictionary<string, StampSignature>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, StampSignature>();

        public static void Save(Database database, Dictionary<string, StampSignature> signatures)
        {
            // Build all geometry before committing the store, so invalid data cannot replace a usable signature.
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(signatures));
            if (bytes.Length > 16 * 1024 * 1024) throw new ArgumentException("Суммарный размер подписей превышает 16 МБ.");
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var dictionary = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForWrite);
                Xrecord record;
                if (dictionary.Contains(StoreKey)) record = (Xrecord)transaction.GetObject(dictionary.GetAt(StoreKey), OpenMode.ForWrite);
                else { record = new Xrecord(); dictionary.SetAt(StoreKey, record); transaction.AddNewlyCreatedDBObject(record, true); }
                var chunks = new List<TypedValue>();
                for (int offset = 0; offset < bytes.Length; offset += 127)
                    chunks.Add(new TypedValue((int)DxfCode.BinaryChunk, bytes.Skip(offset).Take(127).ToArray()));
                using (var buffer = new ResultBuffer(chunks.ToArray())) record.Data = buffer;
                Synchronize(database, transaction, signatures);
                transaction.Commit();
            }
        }

        public static void Synchronize(Database database)
        {
            if (!HasFormat(database)) return;
            var signatures = LoadCached(database);
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                Synchronize(database, transaction, signatures);
                transaction.Commit();
            }
        }

        private static void Synchronize(Database database, Transaction transaction, Dictionary<string, StampSignature> signatures)
        {
            var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            if (!table.Has(FormatInsertService.BlockName)) return;
            var definition = (BlockTableRecord)transaction.GetObject(table[FormatInsertService.BlockName], OpenMode.ForRead);
            if (definition.IsFromExternalReference) return;
            var definitions = new List<ObjectId> { definition.ObjectId };
            if (definition.IsDynamicBlock) definitions.AddRange(definition.GetAnonymousBlockIds().Cast<ObjectId>());
            foreach (var id in definitions)
            {
                var block = (BlockTableRecord)transaction.GetObject(id, OpenMode.ForRead);
                var attributes = new Dictionary<string, AttributeDefinition>(StringComparer.OrdinalIgnoreCase);
                var oldSignatures = new Dictionary<string, List<BlockReference>>();
                foreach (ObjectId entityId in block)
                {
                    var entity = transaction.GetObject(entityId, OpenMode.ForRead);
                    if (entity is AttributeDefinition attribute) attributes[attribute.Tag] = attribute;
                    if (!(entity is BlockReference reference) || reference.ExtensionDictionary.IsNull) continue;
                    var markers = (DBDictionary)transaction.GetObject(reference.ExtensionDictionary, OpenMode.ForRead);
                    if (!markers.Contains(MarkerKey)) continue;
                    var marker = (Xrecord)transaction.GetObject(markers.GetAt(MarkerKey), OpenMode.ForRead);
                    using (var data = marker.Data)
                    {
                        var key = data?.AsArray().FirstOrDefault().Value as string;
                        if (key == null) continue;
                        if (!oldSignatures.ContainsKey(key)) oldSignatures[key] = new List<BlockReference>();
                        oldSignatures[key].Add(reference);
                    }
                }
                var keys = signatures.Keys.Union(oldSignatures.Keys).ToArray();
                foreach (var key in keys)
                {
                    bool signer = key.StartsWith("Signer", StringComparison.Ordinal);
                    bool revision = key.StartsWith("Revision", StringComparison.Ordinal);
                    if ((!signer && !revision) || !int.TryParse(key.Substring(signer ? 6 : 8), out int row)) continue;
                    string tag = signer ? "ФИО" + row : "Nдок." + row;
                    bool visible = attributes.TryGetValue(tag, out var anchor) && !anchor.Invisible;
                    signatures.TryGetValue(key, out var signature);
                    oldSignatures.TryGetValue(key, out var previous);
                    ObjectId geometry = ObjectId.Null;
                    Point3d position = Point3d.Origin;
                    double scale = 1;
                    if (signature != null && visible)
                    {
                        geometry = GetGeometry(database, transaction, signature, out int width, out int height);
                        scale = Math.Min(13.0 / width, 3.5 / height);
                        double left = anchor.Position.X + (signer ? 20 : 6.75);
                        position = new Point3d(left + (13 - width * scale) / 2, anchor.Position.Y - 0.5 + (3.5 - height * scale) / 2, anchor.Position.Z);
                    }
                    if (previous?.Count == 1 && geometry == previous[0].BlockTableRecord &&
                        previous[0].Position.DistanceTo(position) < 1e-7 && Math.Abs(previous[0].ScaleFactors.X - scale) < 1e-7) continue;
                    if (previous != null) foreach (var reference in previous) { reference.UpgradeOpen(); reference.Erase(); }
                    if (geometry.IsNull) continue;
                    if (!block.IsWriteEnabled) block.UpgradeOpen();
                    var inserted = new BlockReference(position, geometry) { ScaleFactors = new Scale3d(scale) };
                    inserted.SetDatabaseDefaults(database);
                    inserted.Layer = "0";
                    block.AppendEntity(inserted); transaction.AddNewlyCreatedDBObject(inserted, true);
                    inserted.CreateExtensionDictionary();
                    var dictionary = (DBDictionary)transaction.GetObject(inserted.ExtensionDictionary, OpenMode.ForWrite);
                    var marker = new Xrecord(); dictionary.SetAt(MarkerKey, marker); transaction.AddNewlyCreatedDBObject(marker, true);
                    using (var data = new ResultBuffer(new TypedValue((int)DxfCode.Text, key))) marker.Data = data;
                }
            }
        }

        private static bool HasFormat(Database database)
        {
            using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
                return ((BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead)).Has(FormatInsertService.BlockName);
        }

        private static ObjectId GetGeometry(Database database, Transaction transaction, StampSignature signature, out int width, out int height)
        {
            if (signature.Png == null || signature.Png.Length == 0) throw new ArgumentException("Подпись не содержит PNG.");
            string name = "AT_SIG_" + Convert.ToHexString(SHA256.HashData(signature.Png)).Substring(0, 32);
            var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            // An existing signature block needs only its size: the PNG is decoded once per session.
            if (table.Has(name) && GeometrySizes.TryGetValue(name, out var known))
            {
                width = known.Width; height = known.Height;
                return table[name];
            }
            BitmapSource image = signature.Image;
            double factor = Math.Min(1, Math.Min(600.0 / image.PixelWidth, 180.0 / image.PixelHeight));
            if (factor < 1) image = new TransformedBitmap(image, new ScaleTransform(factor, factor));
            var bitmap = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            width = bitmap.PixelWidth; height = bitmap.PixelHeight;
            GeometrySizes[name] = (width, height);
            if (table.Has(name)) return table[name];
            if (!table.IsWriteEnabled) table.UpgradeOpen();
            var geometry = new BlockTableRecord { Name = name, Origin = Point3d.Origin };
            table.Add(geometry); transaction.AddNewlyCreatedDBObject(geometry, true);
            int stride = width * 4;
            var pixels = new byte[stride * height]; bitmap.CopyPixels(pixels, stride, 0);
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                int x = 0;
                while (x < width)
                {
                    while (x < width && pixels[y * stride + x * 4 + 3] < 80) x++;
                    int start = x;
                    while (x < width && pixels[y * stride + x * 4 + 3] >= 80) x++;
                    if (x == start) continue;
                    if (++count > 20000) throw new ArgumentException("Слишком сложное изображение подписи. Удалите фон и лишние детали PNG.");
                    var solid = new Solid(new Point3d(start, height - y - 1, 0), new Point3d(x, height - y - 1, 0),
                        new Point3d(start, height - y, 0), new Point3d(x, height - y, 0));
                    solid.SetDatabaseDefaults(database); solid.Layer = "0";
                    solid.Color = AcadColor.FromRgb(SignatureLibrary.InkColor.R, SignatureLibrary.InkColor.G, SignatureLibrary.InkColor.B);
                    geometry.AppendEntity(solid); transaction.AddNewlyCreatedDBObject(solid, true);
                }
            }
            if (count == 0) throw new ArgumentException("Подпись не содержит видимых штрихов.");
            return geometry.ObjectId;
        }
    }
}
