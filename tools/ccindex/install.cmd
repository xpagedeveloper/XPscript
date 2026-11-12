@echo off
setlocal EnableExtensions

rem Install the modified ccindex package and index the XPscript repository.
set "SCRIPT_DIR=%~dp0"
set "PROJECT_ROOT=%SCRIPT_DIR%..\.."
set "PACKAGE_DIR=%SCRIPT_DIR%ccindex-runtime"
set "VENV_DIR=%SCRIPT_DIR%.venv"
set "MODEL_ROOT=%USERPROFILE%\.ccindex\models"
set "POTION_DIR=%MODEL_ROOT%\potion-code-16m-v2"
set "RERANKER_DIR=%MODEL_ROOT%\reranker-onnx"
set "ZIP_FILE=%SCRIPT_DIR%ccindex-modified.zip"

where py >nul 2>nul
if errorlevel 1 (
  echo Python launcher ^(py^) is required.
  exit /b 1
)
where curl.exe >nul 2>nul
if errorlevel 1 (
  echo curl.exe is required.
  exit /b 1
)
if not exist "%ZIP_FILE%" (
  echo Missing %ZIP_FILE%.
  exit /b 1
)

if exist "%PACKAGE_DIR%" rmdir /s /q "%PACKAGE_DIR%"
mkdir "%PACKAGE_DIR%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -LiteralPath '%ZIP_FILE%' -DestinationPath '%PACKAGE_DIR%' -Force"
if errorlevel 1 exit /b 1

if not exist "%VENV_DIR%\Scripts\python.exe" py -3 -m venv "%VENV_DIR%"
if errorlevel 1 exit /b 1
"%VENV_DIR%\Scripts\python.exe" -m pip install --upgrade pip
if errorlevel 1 exit /b 1
"%VENV_DIR%\Scripts\python.exe" -m pip install -e "%PACKAGE_DIR%"
if errorlevel 1 exit /b 1

mkdir "%POTION_DIR%" 2>nul
for %%F in (model.safetensors tokenizer.json config.json) do (
  if not exist "%POTION_DIR%\%%F" curl.exe -L --fail --retry 3 "https://huggingface.co/minishlab/potion-code-16M-v2/resolve/main/%%F" -o "%POTION_DIR%\%%F"
  if errorlevel 1 exit /b 1
)

mkdir "%RERANKER_DIR%" 2>nul
if not exist "%RERANKER_DIR%\model.onnx" curl.exe -L --fail --retry 3 "https://github.com/dillibabukadati/ccindex/releases/latest/download/reranker-onnx-model.onnx" -o "%RERANKER_DIR%\model.onnx"
if errorlevel 1 exit /b 1
for %%F in (tokenizer.json tokenizer_config.json special_tokens_map.json) do (
  if not exist "%RERANKER_DIR%\%%F" curl.exe -L --fail --retry 3 "https://huggingface.co/cross-encoder/ms-marco-MiniLM-L-6-v2/resolve/main/%%F" -o "%RERANKER_DIR%\%%F"
  if errorlevel 1 exit /b 1
)

pushd "%PROJECT_ROOT%"
"%VENV_DIR%\Scripts\ccindex.exe" index
set "RESULT=%ERRORLEVEL%"
popd
if not "%RESULT%"=="0" exit /b %RESULT%

echo ccindex installation and XPscript indexing completed.
exit /b 0
