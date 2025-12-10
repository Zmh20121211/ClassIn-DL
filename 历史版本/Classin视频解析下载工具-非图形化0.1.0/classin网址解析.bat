@echo off
:: 设置字符编码为UTF-8
chcp 65001 >nul

:: 清除现有输出文件
for /r %%i in ("Output.ini") do (
type nul > %%i 
)

:: 清屏
cls

:: 请求用户输入，选择操作
set /p input="输入【1】开始解析请求头，输入【2】显示结果后退出，按【Enter】确认："
if /i %input%==1 goto Url
if /i %input%==2 goto Exit
if /i %input%==3 goto Print

:: 解析URL
:Url
echo 正在解析...

:: 从剪贴板获取请求头数据并保存到临时文件
mshta "javascript:var s=clipboardData.getData('text');if(s)new ActiveXObject('Scripting.FileSystemObject').GetStandardStream(1).Write(s);close();"|more >InputStaging.dll

:: 从请求头中提取URL
for /f "tokens=2 delims= " %%U in ('findstr "Url" InputStaging.dll') do (
    set Url=%%U
)

:: 清理URL中的特殊字符
set "Url=%Url:\=%"
set "Url=%Url:"=%"

:: 检查URL是否包含https://
echo %Url% | findstr /c:"https://" >nul
if %errorlevel% equ 0 (
    :: URL有效，保存结果
    echo %Url%>>Input.dll
    echo %Url%>>Output.ini
    echo %Url%
    echo 成功！
) else (
    :: URL无效，显示错误信息
    echo 错误行：%Url%
    echo 不包含请求头格式，请检查请求头格式
)

:: 请求用户输入，继续解析或终止
set /p input="输入【1】继续解析请求头，输入【2】显示结果后退出，按【Enter】确认："
if /i %input%==1 goto Url
if /i %input%==2 goto Exit

:: 退出程序，显示结果
:Exit
:: 显示解析结果
for /f "delims=[" %%i in (Output.ini) do echo %%i
:: 暂停程序，等待用户查看结果
TIMEOUT /T -1