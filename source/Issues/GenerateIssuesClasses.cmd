pushd %~dp0
texttransform ParsingIssues.tt -out ..\LottieReader\Serialization\ParsingIssues.cs
texttransform TranslationIssues.tt -out ..\LottieToWinComp\TranslationIssues.cs
texttransform ValidationIssues.tt -out ..\LottieData\ValidationIssues.cs
popd