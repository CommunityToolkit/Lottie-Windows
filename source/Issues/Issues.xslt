<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="html" encoding="utf-8" indent="yes" />

  <xsl:template match="/">
    <xsl:text disable-output-escaping='yes'>&lt;!DOCTYPE html&gt;</xsl:text>
    <html>
      <head>
        <title>Lottie-Windows warnings and errors</title>
      </head>
      <body>
        <div>List of errors and warnings.</div>
        <table>
          <xsl:for-each select="issues/issue">
            <tr>
              <td>
                <a>
                  <xsl:attribute name="name">
                    <xsl:value-of select="id"/>
                  </xsl:attribute>
                </a>
                <xsl:value-of select="id"/>
              </td>
              <td>
                <xsl:value-of select="text"/>
              </td>
            </tr>
          </xsl:for-each>
        </table>
      </body>
    </html>
  </xsl:template>

</xsl:stylesheet>