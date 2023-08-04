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
find . -name obj | grep -v node_modules | xargs rm -rf
find . -name bin | grep -v node_modules | xargs rm -rf
set -e
echo "Building for full-size Linux distros ..."
dotnet publish -v d -r linux-x64 -c Release --self-contained --force /p:PublishSingleFile=true SafeguardDevOpsService/SafeguardDevOpsService.csproj
echo "Building for tiny Linux distros ..."
dotnet publish -v d -r linux-musl-x64 -c Release --self-contained --force /p:PublishSingleFile=true SafeguardDevOpsService/SafeguardDevOpsService.csproj

DockerFile=`get_safeguard_dockerfile $ImageType`
ImageName="oneidentity/safeguard-devops:$Version$ImageType"

if [ ! -z "$(docker images -q $ImageName)" ]; then
    echo "Cleaning up the old image: $ImageName ..."
    docker rmi --force "$ImageName"
fi
echo "Building a new image: $ImageName ..."
docker build --no-cache -t "$ImageName" -f "docker/$DockerFile" $ScriptDir 2>&1
set +e
