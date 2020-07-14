#!/bin/bash
trap "exit 1" TERM
export TOP_PID=$$

print_usage()
{
    cat <<EOF
USAGE: run-docker.sh [-h] [-b image] [-p port] [-c command]
  -h  Show help and exit
  -b  Base image type to use (default: alpine)
  -p  Port number to expose service on (default: 443)
  -c  Alternate command to run in the container (-c /bin/bash to get a prompt)
      Always specify the -c option last (most useful with -i)
EOF
    exit 0
}

ScriptDir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
Port=443
if [ -z "$1" ]; then
    ImageType=alpine
else
    ImageType=$1
fi

while getopts ":cp:b:h" opt; do
    case $opt in
    h)
        print_usage
        ;;
    b)
        ImageType=$OPTARG
        shift; shift
        ;;
    p)
        Port=$OPTARG
        regex='^[0-9]+$'
        if ! [[ "$Port" =~ $regex ]] ; then
            >&2 echo "Non-numeric port provided '$Port'"
            exit 1
        fi
        shift; shift
        ;;
    ?)
        break
        ;;
    esac
done


# Make sure docker is installed
if [ -z "$(which docker)" ]; then
    >&2 echo "You must install docker to use this script"
    exit 1
fi

set -e
. "$ScriptDir/docker/docker-include.sh"
DockerFile=`get_safeguard_dockerfile $ImageType`

ImageName="oneidentity/safeguard-devops:$ImageType"
ContainerName="safeguard-devops-runtime"

echo "Rebuilding the image: $ImageName ..."
$ScriptDir/build-docker.sh $ImageType
set +e

echo "Clean up any old container named $ContainerName ..."
docker ps -a | grep $ContainerName
if [ $? -eq 0 ]; then
    docker rm $ContainerName
fi

echo -e "Running interactive container ($ContainerName) for $ImageName on port $Port ..."
docker run \
    --name $ContainerName \
    -p $Port:4443 \
    --cap-add NET_ADMIN \
    -it "$ImageName" "$@"
