# Generate the HTML version of the issues.
$outputPath = '.\WarningsAndErrors.html'
"Generating to $outputPath"

$xslt = New-Object System.Xml.Xsl.XslCompiledTransform;
$xslt.Load('.\Issues.xslt');
$xslt.Transform('.\Issues.xml', $outputPath);
