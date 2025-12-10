@echo off
:: 设置字符编码为UTF-8
chcp 65001 >nul

:: 清除现有解析结果文件
for /r %%i in ("解析视频结果.ini") do (
type nul > %%i 
)

:: 清除现有URL解析结果文件
for /r %%i in ("URL解析结果.ini") do (
type nul > %%i 
)

:: 请求用户输入，选择操作
set /p input="输入【1】开始解析请求头，输入【2】显示结果后退出，按【Enter】确认："
if /i %input%==1 goto Url
if /i %input%==2 goto Exit
if /i %input%==3 goto Print

:: 解析URL和课程名称
:Url
:: 从剪贴板获取请求头数据并保存到临时文件
mshta "javascript:var s=clipboardData.getData('text');if(s)new ActiveXObject('Scripting.FileSystemObject').GetStandardStream(1).Write(s);close();"|more >请求头缓存.dll

:: 从请求头中提取URL
for /f "tokens=2 delims= " %%U in ('findstr "Url" 请求头缓存.dll') do (
    set Url=%%U
)

:: 从请求头中提取课程名称
for /f "tokens=2 delims=:,'" %%N in ('findstr "lessonName" 请求头缓存.dll') do (
    set Name=%%N
)

:: 清理课程名称中的特殊字符
set "Name=%Name: =%"
set "Name=%Name:"=%"

:: 清理URL中的特殊字符
set "Url=%Url:\=%"
set "Url=%Url: =%"
set "Url=%Url:"=%"

:: 构造下载命令
set Download="aria.exe" -d "D:\Desktop" -o "%Name%.mp4" "%Url%"

:: 检查URL是否包含https://
echo %Url% | findstr /c:"https://" >nul
if %errorlevel% equ 0 (
    :: URL有效，保存结果
    echo %Name%>>解析视频结果.ini
    echo %Url%>>URL解析结果.ini
    echo %Download%>>下载视频.bat
    echo 解析成功！
) else (
    :: URL无效，显示错误信息
    echo 解析失败！错误原因：不包含请求头格式   错误行：%lesson%
)

:: 请求用户输入，继续解析或终止
set /p input="输入【1】继续解析请求头，输入【2】解析完成后退出，按【Enter】确认："
if /i %input%==1 goto Url
if /i %input%==2 goto Exit

:: 退出程序
:Exit
TIMEOUT /T -1