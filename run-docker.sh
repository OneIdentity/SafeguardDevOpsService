#!/bin/bash
trap "exit 1" TERM
export TOP_PID=$$

Port=443
ScriptDir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

if [ -z "$1" ]; then
    ImageType=alpine
else
    ImageType=$1
fi

# Make sure docker is installed
if [ -z "$(which docker)" ]; then
    >&2 echo "You must install docker to use this script"
    exit 1
fi

. "$ScriptDir/docker/docker-include.sh"
DockerFile=`get_safeguard_dockerfile $ImageType`

ImageName="oneidentity/safeguard-devops:$ImageType"
ContainerName="safeguard-devops-runtime"

echo "Rebuilding the image: $ImageName ..."
$ScriptDir/build-docker.sh $ImageType

# Clean up any old container with that name
docker ps -a | grep $ContainerName
if [ $? -eq 0 ]; then
    docker rm $ContainerName
fi

echo "Running the image: $ImageName ..."
docker run \
    --name $ContainerName \
    -p $Port:443 \
    -it $ImageName "$@"
