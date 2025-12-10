@echo off&setlocal EnableDelayedExpansion
:: 设置字符编码为UTF-8
chcp 65001

:: 清除现有解析结果文件
for /r %%i in ("解析结果.ini") do (
type nul > %%i 
)

:: 清除现有下载工具脚本
for /r %%i in ("下载视频工具.bat") do (
type nul > %%i 
)

:: 初始化计数器
set Number=1

:: 将getLessonRecordInfo字符串写入剪贴板
set/p="getLessonRecordInfo"<nul | clip
echo 提示：已将getLessonRecordInfo字符串写入粘贴板

:: 初始化下载视频工具脚本
 echo @echo off>>下载视频工具.bat
echo chcp 65001>>下载视频工具.bat

:: 请求用户输入，开始解析程序
set /p input="输入“1”开始解析程序、按【Enter】确认："
if /i %input%==1 goto Url

:: 解析URL和课程名称
:Url
:: 从剪贴板获取请求头数据并保存到临时文件
mshta "javascript:var s=clipboardData.getData('text');if(s)new ActiveXObject('Scripting.FileSystemObject').GetStandardStream(1).Write(s);close();"|more >请求头缓存.dll

:: 从请求头中提取URL
for /f "tokens=3 delims= " %%U in ('findstr "Url" 请求头缓存.dll') do (
    set Url=%%U
)

:: 从请求头中提取课程名称
for /f "tokens=2 delims=:,'" %%N in ('findstr "lessonName" 请求头缓存.dll') do (
    set Name=%%N
)

:: 清理课程名称中的特殊字符
set "Name=%Name: =%"
set "Name=%Name:"=%"
set "Name=%Name:\=%"
set "Name=%Name:/=%"

:: 清理URL中的特殊字符
set "Url=%Url:\=%"
set "Url=%Url: =%"
set "Url=%Url:"=%"

:: 组合课程名称和URL
set Class=%Name% — %Url%

:: 检查URL是否包含https://
echo %Class% | findstr /c:"https://" >nul
if %errorlevel% equ 0 (
    :: URL有效，增加计数并保存结果
    set /a Number+=1
    echo %Class%>>解析结果.ini
    :: 添加下载命令到下载工具脚本
    echo "aria2c.exe" -d "%CD%\下载目录" -o "%Name%.mp4" "%Url%">>下载视频工具.bat
    echo 解析成功，目前已解析%Number%个视频，解析结果：%Class%
) else (
    :: URL无效，显示错误信息
    echo 解析失败！错误原因：不包含请求头格式   错误行：%Class%
)

:: 请求用户输入，继续解析或终止
set /p input="按下“Enter”解析请求头，输入“2”终止解析、按【Enter】确认："
if /i %input%==1 goto Url
if /i %input%==2 goto Exit

:: 退出解析，准备下载
:Exit
set /p input="按下“Enter”键开始下载，或手动检查错误后开始下载（提示：下载完成后可在“%CD%\下载目录”文件夹观看视频）："
if /i %input%==1 goto Downloads

:: 执行下载
:Downloads
start 下载视频工具.bat