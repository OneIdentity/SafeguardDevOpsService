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

check_ip_address()
{
    if ! [[ $1 =~ ^((1?[0-9]{1,2}|2[0-4][0-9]|25[0-5])\.){3}(1?[0-9]{1,2}|2[0-4][0-9]|25[0-5])$ ]]; then
        >&2 echo "'$1' must be a valid IP address"
        exit 1
    fi
}

ScriptDir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
IPAddress=
Port=443

while getopts ":ci:p:b:h" opt; do
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
    i)
        IPAddress=$OPTARG
        check_ip_address $IPAddress
        shift; shift
        ;;
    ?)
        break
        ;;
    esac
done

if [ -z "$ImageType" ]; then
    ImageType=alpine
fi

if [ -z "$IPAddress" ]; then
    if [ "$(uname)" = "Darwin" ]; then
        IPAddress=$(ifconfig | grep inet | grep -v inet6 | cut -d' ' -f2 | tr '\n' ',')
    else
        IPAddress=$(ip -o route get to 8.8.8.8 | sed -n 's/.*src \([0-9.]\+\).*/\1/p' | tr '\n' ',')
    fi
    IPAddress=${IPAddress%,}
fi

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
    --env DOCKER_HOST_IP=$IPAddress \
    --cap-add NET_ADMIN \
    -it "$ImageName" "$@"
