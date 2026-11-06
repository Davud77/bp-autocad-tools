param([ValidateSet(2026,2027)][int]$Year=2026)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$framework=if($Year -eq 2027){'net10.0-windows'}else{'net8.0-windows'}
$reports=Join-Path $root ('artifacts/validation/features-'+$Year+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$plugin=Join-Path $root "bin/Release/AutoCAD$Year/$framework/BotPlusAutocadTool.dll"
dotnet build (Join-Path $root 'BotPlusAutocadTool.csproj') -c Release "-p:AutoCADVersion=$Year" -v quiet
if($LASTEXITCODE -ne 0){throw 'Plugin build failed'}
$cases=@(
    @{Name='NumberedCopyChecks';Commands=@('NUMBERED_COPY_CHECKS','NUMBERED_COPY_CHECK_PAPER','NUMBERED_COPY_SEED_MANUAL','NUMBERED_COPY_MANUAL','10,20,3','40,20,3','70,20,3','_Undo','100,20,3','','NUMBERED_COPY_VERIFY_MANUAL','NUMBERED_COPY_SEED_CURVE','NUMBERED_COPY_CURVE','_Last','_No','NUMBERED_COPY_VERIFY_CANCEL','NUMBERED_COPY_CURVE','_Last','_Yes','NUMBERED_COPY_VERIFY_CURVE','_U','NUMBERED_COPY_VERIFY_UNDO');Env='AUTOCADTOOLS_NUMCOPY_SCRATCH';Markers=@('NUMBERED_COPY_PASS:','PAPER_PASS:','MANUAL_PASS:','CANCEL_PASS:','CURVE_PASS:','UNDO_PASS:')},
    @{Name='DrawingSymbolChecks';Commands=@('DRAWING_SYMBOL_CHECKS');Env='AUTOCADTOOLS_SYMBOL_SCRATCH';Markers=@('PASS')},
    @{Name='StampChecks';Commands=@('STAMP_CHECKS');Env='AUTOCADTOOLS_STAMP_SCRATCH';Markers=@('PASS')},
    @{Name='MultiCopyChecks';Commands=@('MULTICOPY_CHECKS','MULTICOPY_SEED','ATMCOPY','_Count','3','_Curve','_Last','MULTICOPY_VERIFY_COMMAND','MULTICOPY_PRESELECT','ATMCOPY','_Step','30','0,0','100,0','MULTICOPY_VERIFY_POINTS','MULTICOPY_SEED_UCS','ATMCOPY','30','10,20,3','100,20,3','MULTICOPY_VERIFY_UCS');Env='BOTPLUS_CHECK_DIRECTORY';Markers=@('MULTICOPY_PASS:')},
    @{Name='AlignSkewChecks';Commands=@('ALIGN_SKEW_CHECKS');Env='BOTPLUS_CHECK_DIRECTORY';Markers=@('ALIGN_SKEW_PASS:')}
)
foreach($case in $cases){
    $folder=Join-Path $reports $case.Name
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    dotnet build (Join-Path $root ('tests/'+$case.Name)) -c Release "-p:AutoCADVersion=$Year" -v quiet *> (Join-Path $folder 'build.log')
    if($LASTEXITCODE -ne 0){throw "Feature module build failed: $folder"}
    Copy-Item -LiteralPath (Join-Path $root 'Assets/WM_FORMAT.dwg') -Destination (Join-Path $folder 'WM_FORMAT.dwg')
    $module=Join-Path $root "tests/$($case.Name)/bin/Release/$framework/$($case.Name).dll"
    $script=Join-Path $folder 'test.scr'
    $lines=@('SECURELOAD','0','_NETLOAD',('"'+$plugin+'"'),'_NETLOAD',('"'+$module+'"'))+$case.Commands+@('_QUIT','_Y','')
    [IO.File]::WriteAllLines($script,$lines,[Text.Encoding]::ASCII)
    $prior=[Environment]::GetEnvironmentVariable($case.Env)
    $priorData=$env:BOTPLUS_DATA_DIRECTORY
    $priorTemplate=$env:AUTOCADTOOLS_STAMP_TEMPLATE
    try{
        [Environment]::SetEnvironmentVariable($case.Env,$folder)
        $env:BOTPLUS_DATA_DIRECTORY=Join-Path $folder 'data'
        New-Item -ItemType Directory -Path $env:BOTPLUS_DATA_DIRECTORY -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $env:BOTPLUS_DATA_DIRECTORY 'settings.json'),'{"AutoCheckUpdates":false}')
        $env:AUTOCADTOOLS_STAMP_TEMPLATE=Join-Path $root 'Assets/WM_FORMAT.dwg'
        $process=Start-Process -FilePath "C:/Program Files/Autodesk/AutoCAD $Year/accoreconsole.exe" -ArgumentList @('/s',('"'+$script+'"'),'/l','en-US','/isolate','BotPlusFeatures',('"'+(Join-Path $folder 'profile')+'"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $folder 'stdout.log') -RedirectStandardError (Join-Path $folder 'stderr.log')
        if(!$process.WaitForExit(60000)){Stop-Process -Id $process.Id;throw "Timeout: $folder"}
    }finally{
        [Environment]::SetEnvironmentVariable($case.Env,$prior)
        $env:BOTPLUS_DATA_DIRECTORY=$priorData
        $env:AUTOCADTOOLS_STAMP_TEMPLATE=$priorTemplate
    }
    $log=Join-Path $folder 'results.log'
    if(!(Test-Path -LiteralPath $log)){throw "No feature results: $folder"}
    $text=[IO.File]::ReadAllText($log)
    if($text -match 'FAIL|Exception'){throw $text}
    foreach($marker in $case.Markers){if(!$text.Contains($marker)){throw "Missing $marker in $log"}}
    $text.Trim()
}
Write-Output "Reports: $reports"
