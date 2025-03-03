#Purpose:  Updates SNI Version
Write-Host "SNI Version to test = 123"

##Get the shared SNI Version from the downloaded artifact
#$SharedSNIVersion = Get-Content -path "$(Pipeline.Workspace)/SharedSNIVersion.txt"

#Get the SNI Version to test from the user entered version.
$SharedSNIVersion = "123"

# define file to update
$PropsPath = 'C:\Users\mdaigle\SqlClient\tools\props\Versions.props'
type $PropsPath

# new version number to update to
##Write-Host "SNI Version to test = $(SNIValidationVersion)"
Write-Host "SNI Version to test = $SharedSNIVersion"


# define an xml object
$xml = New-Object XML

# load content of xml from file defined above
$xml.Load($PropsPath)

# define namespace used to read a node 
$nsm = New-Object Xml.XmlNamespaceManager($xml.NameTable)
$nsm.AddNamespace('ns', $xml.DocumentElement.NamespaceURI)
$netFxSniVersion = $xml.SelectSingleNode('//ns:MicrosoftDataSqlClientSniVersion', $nsm)

Write-Host "Node NetFx SNI Version = $($netFxSniVersion.InnerText)"

# update the node inner text
$netFxSniVersion.InnerText = "$SharedSNIVersion"

$netCoreSniVersion = $xml.SelectSingleNode('//ns:MicrosoftDataSqlClientSNIRuntimeVersion', $nsm)

# update the node inner text
$netCoreSniVersion.InnerText = "$SharedSNIVersion"

# save the xml file
$xml.Save($PropsPath)

type $PropsPath
