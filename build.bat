REM Build the project exe
"C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe" "E:\Programming\!Csharp\ftplib\ftplib.sln" /property:Configuration=Release

REM Generate SandCastle Documentation
"C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe" "E:\Programming\!Csharp\ftplib\ftpdocs\ftpdocs.shfbproj"

REM Clean up
COPY "E:\Programming\!Csharp\ftplib\ftpdocs\Presentation.css" "E:\Programming\!Csharp\ftplib\ftpdocs\Help\styles"
COPY "E:\Programming\!Csharp\ftplib\ftpdocs\TOC.css" "E:\Programming\!Csharp\ftplib\ftpdocs\Help"
"C:\Program Files\7-Zip\7z.exe" a -mx9 "E:\Programming\!Csharp\ftplib\ftpdocs.7z" "E:\Programming\!Csharp\ftplib\ftpdocs\Help"

exit