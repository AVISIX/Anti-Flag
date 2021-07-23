@echo off 

:: Create the Final Output Folder
set outputFolder=%cd%\Final\
IF NOT EXIST %outputFolder% (
    mkdir %outputFolder%
)

:: Create the Temporary Folder which will later be zipped up.
set tempFolder=%cd%\Final\AntiFlagV2\
IF NOT EXIST %tempFolder% ( 
    mkdir %tempFolder%
)

SET buildPath=%cd%\AntiFlagV2\bin\Release\
SET obfuscatedSarah=%buildPath%\Obfuscated\AntiFlag.dll

IF NOT EXIST %buildPath%net5.0\win-x64\AntiFlag.dll (
    echo Please Build the Project in release mode before running this Script. 
    PAUSE 
    goto endScript
)

IF EXIST %obfuscatedSarah% (
    echo Obfuscated DLL found, replacing..
    DEL =%buildPath%\net5.0\win-x64\AntiFlag.dll
    copy %obfuscatedSarah% =%buildPath%\net5.0\win-x64\
)

:: Publish the Project 
dotnet publish %cd%\AntiFlagV2\AntiFlagV2.csproj -f net5.0 -r win-x64 -c Release --no-build -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true --self-contained true -o %tempFolder%

:: Copy the Manual 
copy %cd%\..\readme.pdf %tempFolder%

:: Cleanup 
del %tempFolder%AntiFlag.pdb 
del %tempFolder%SXAuth.pdb 
del %tempFolder%SXAntiDebug.pdb

IF EXIST %outputFolder%Sarah.zip (
    del %outputFolder%Sarah.zip 
)

WinRAR a -ep1 -idq -r -y %outputFolder%Anti_Flag.zip "%tempFolder%"

rmdir /Q /S %tempFolder%

endScript: