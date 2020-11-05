#!/bin/bash
if [ "$#" -ne 3 ]; then
    >&2 echo "This script requires 3 arguments -- sourceDir, semVer, buildId"
    exit 1
fi
sourceDir=$1
semVer=$2
buildId=$3

echo "semVer = $semVer"
echo "buildId = $buildId"

buildNumber=$(expr $buildId % 65535) # max value for version part on Windows is 65534
echo "buildNumber = $($local:buildNumber)"

versionString="$semVer.$buildNumber"
templateVersion = "255.255.65534.65534"
echo "versionString = $versionString"
echo "templateVersion = $templateVersion"

echo "Searching for AssemblyInfo.cs files in '$sourceDir'"
find $sourceDir -name AssemblyInfo.cs -print0 | while read -d $'\0' file
do
    echo "Replacing version information in '$file'"
    sed -i "s/$templateVersion/$versionString" $file
    echo "*****"
    cat $file
    echo "*****"
done

echo "##vso[task.setvariable variable=VersionString;]$versionString"
