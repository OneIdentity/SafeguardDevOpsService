#!/bin/bash
if [ "$#" -ne 4 ]; then
    >&2 echo "This script requires 3 arguments -- sourceDir, semVer, buildId"
    exit 1
fi
sourceDir=$1
semVer=$2
buildId=$3
isPrerelease=$(echo "$4" | tr '[:upper:]' '[:lower:]')

echo "semVer = $semVer"
echo "buildId = $buildId"

buildNumber=$(expr $buildId % 65535) # max value for version part on Windows is 65534
echo "buildNumber = ${buildNumber}"

packageCodeMarker="255.255.65534"
assemblyCodeMarker="255.255.65534.65534"
assemblyVersion="$semVer.$buildNumber"
if $isPrerelease; then
    packageVersion="$semVer-dev-$buildNumber"
else
    packageVersion="$semVer"
fi

echo "packageCodeMarker = $packageCodeMarker"
echo "assemblyCodeMarker = $assemblyCodeMarker"
echo "assemblyVersion = $assemblyVersion"
echo "packageVersion = $packageVersion"

echo "Replacing version information in SafeguardDevOpsService assembly info"
projectFile="$sourceDir/SafeguardDevOpsService/Properties/AssemblyInfo.cs"
sed -i "s/$assemblyCodeMarker/$assemblyVersion/" $projectFile
sed -i "s/$packageCodeMarker/$packageVersion/" $projectFile
echo "*****"
cat $projectFile
echo "*****"

echo "Replacing version information in DevOpsPluginCommon project file"
projectFile="$sourceDir/DevOpsPluginCommon/DevOpsPluginCommon.csproj"
sed -i "s/$assemblyCodeMarker/$assemblyVersion/" $projectFile
sed -i "s/$packageCodeMarker/$packageVersion/" $projectFile
echo "*****"
cat $projectFile
echo "*****"

echo "Replacing version information in DevOpsAddonCommon project file"
projectFile="$sourceDir/DevOpsAddonCommon/DevOpsAddonCommon.csproj"
sed -i "s/$assemblyCodeMarker/$assemblyVersion/" $projectFile
sed -i "s/$packageCodeMarker/$packageVersion/" $projectFile
echo "*****"
cat $projectFile
echo "*****"

echo "##vso[task.setvariable variable=VersionString;]$assemblyVersion"
