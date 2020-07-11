#!/bin/bash
trap "exit 1" TERM
export TOP_PID=$$

if [ -z "$1" ]; then
    ImageType=alpine
else
    ImageType=$1
fi

if [ ! -z "$2" ]; then
    Version="${2}-"
fi

CurDir=$(pwd)
ScriptDir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
. "$ScriptDir/docker/docker-include.sh"

cleanup()
{
    cd $CurDir
}
trap cleanup EXIT

if [ -z "$(which dotnet)" ]; then
    >&2 echo "This script requires dotnet cli for building the service"
    exit 1
fi

cd $ScriptDir
echo "Cleaning up all build directories ..."
find . -name obj | xargs rm -rf
find . -name bin | xargs rm -rf
echo "Building for full-size Linux distros ..."
dotnet publish -r linux-x64 -c Release --self-contained --force /p:PublishSingleFile=true SafeguardDevOpsService/SafeguardDevOpsService.csproj
echo "Building for tiny Linux distros ..."
dotnet publish -r linux-musl-x64 -c Release --self-contained --force /p:PublishSingleFile=true SafeguardDevOpsService/SafeguardDevOpsService.csproj

DockerFile=`get_safeguard_dockerfile $ImageType`

if [ ! -z "$(docker images -q oneidentity/safeguard-devops:$Version$ImageType)" ]; then
    echo "Cleaning up the old image: oneidentity/safeguard-devops:$Version$ImageType ..."
    docker rmi --force "oneidentity/safeguard-devops:$Version$ImageType"
fi
echo "Building a new image: oneidentity/safeguard-devops:$Version$ImageType ..."
docker build --no-cache -t "oneidentity/safeguard-devops:$Version$ImageType" -f "docker/$DockerFile" $ScriptDir
