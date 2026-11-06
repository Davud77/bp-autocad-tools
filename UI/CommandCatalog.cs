// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
namespace BotPlusAutocadTool.UI
{
    internal sealed record ButtonDefinition(string Title, string Icon, string Command, string Description);
    internal sealed record PanelDefinition(string Title, ButtonDefinition[] Buttons);

    internal static class CommandCatalog
    {
        #if BOTPLUS_DESKTOP_PREVIEW
        internal const string TabId = "BOTPLUS_AUTOCAD_TOOL_PREVIEW_TAB";
        internal const string TabTitle = "BotPlus AT 1.8";
#else
        internal const string TabId = "BOTPLUS_AUTOCAD_TOOL_TAB";
        internal const string TabTitle = "BotPlus AT";
#endif

        internal static readonly PanelDefinition[] Panels =
        {
            new("Инструменты", new[]
            {
                new ButtonDefinition("Аксонометрия", "axon.png", "ATISO", "Преобразовать выбранные объекты в аксонометрическую проекцию."),
                new ButtonDefinition("Автонумерация", "autonum.png", "AUTONUM", "Пронумеровать текст, атрибуты или выноски в выбранном порядке."),
                new ButtonDefinition("Выбор по\nобразцу", "selectsim.png", "WSELECT", "Найти объекты по свойствам выбранного образца."),
                new ButtonDefinition("Сумма\nдлин", "sumlen.png", "SUMLENGTH", "Длина, площадь и состав выделения с настройками единиц и масштаба."),
                new ButtonDefinition("Мульти-\nкопирование", "multicopy.png", "MULTICOPY", "Создать несколько копий выбранных объектов."),
                new ButtonDefinition("Копирование с\nнумерацией", "numberedcopy.png", "ATNCOPY", "Копировать текст, атрибуты и выноски с нумерацией по точкам или кривой."),
                new ButtonDefinition("Подготовка\nпланов", "planprep.png", "PLANPREP", "Подготовить выбранные объекты плана по заданным параметрам."),
                new ButtonDefinition("Дополнительные\nфункции ▾", "additional.png", "BOTPLUS_ADDITIONAL", "Дополнительные функции по категориям из встроенной библиотеки.")
            }),
            new("Оформление", new[]
            {
                new ButtonDefinition("Условные\nобозначения", "symbols.png", "ATSYMBOL", "Каталог разрезов, узлов, отметок, осей и подписей."),
                new ButtonDefinition("Разрез", "section.png", "ATSECTION", "Прямой или ступенчатый разрез со стрелками и обозначениями."),
                new ButtonDefinition("Обрыв /\nразрыв", "break.png", "ATBREAK", "Ломаная, волнистая линия или пунктирный разрыв.")
            }),
            new("Таблицы", new[]
            {
                new ButtonDefinition("Таблица\nExcel", "tableedit.png", "ATEXCEL", "Отредактировать таблицу в XLSX и вернуть изменения в чертёж."),
                new ButtonDefinition("Экспорт\nExcel", "excel_export.png", "BOTPLUS_EXPORTEXCEL", "Сохранить выбранную TABLE в XLSX."),
                new ButtonDefinition("Импорт\nExcel", "excel_import.png", "BOTPLUS_IMPORTEXCEL", "Создать TABLE из XLSX или обновить существующую таблицу."),
                new ButtonDefinition("Очистить стиль\nтаблиц", "table_reset.png", "BOTPLUS_CLEARTABLESTYLE", "Снять локальное оформление ячеек и текста в выбранных TABLE, применив текущий стиль каждой таблицы.")
            }),
            new("Очистка и сброс", new[]
            {
                new ButtonDefinition("Очистка\nчертежа", "clean.png", "CLEANDWG", "Удалить выбранные категории лишних данных чертежа."),
                new ButtonDefinition("Очистить\nневыбранные", "clean_invert.png", "CLEANUNSELECTED", "Сохранить выбранные объекты, удалив остальные из заданных пространств."),
                new ButtonDefinition("Сброс\nтекста", "srtimtext.png", "STRIPTEXT", "Снять выбранные переопределения текстового оформления.")
            }),
            new("Выпуск", new[]
            {
                new ButtonDefinition("Штамп", "stamp.png", "ATSTAMP", "Заполнить пользовательские свойства чертежа для штампа."),
                new ButtonDefinition("Вставить\nформатку", "frame.png", "BOTPLUS_INSERTFORMAT", "Вставить динамический блок WM_FORMAT с атрибутами."),
                new ButtonDefinition("Печать", "wplot.png", "ATPLOT", "Выбрать форматки и выпустить PDF со сшивкой или по листам."),
                new ButtonDefinition("Сформировать\nзаказчику", "dwgexport.png", "ATHANDOFF", "Подготовить чертёж или отдельную копию для передачи заказчику.")
            }),
            new("Сервис", new[]
            {
                new ButtonDefinition("Команды и\nсокращения", "aliases.png", "ATALIASES", "Поиск команд и настройка личных сокращений в acad.pgp."),
                new ButtonDefinition("Сервис", "info.png", "ATINFO", "Настройки, проверка обновлений и справка о плагине.")
            })
        };
    }
}
