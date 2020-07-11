# This shouldn't be run directly

print_usage()
{
    >&2 cat <<EOF
USAGE: run-docker.sh [imagetype] [-h]

  -h  Show help and exit

imagetype should be one of the following:

  'ubuntu', 'ubuntu20.04'
  'alpine', 'alpine3.12'

EOF
    kill -s TERM $TOP_PID
}

get_safeguard_dockerfile()
{
    case $1 in
    alpine | alpine3.12)
        DockerFile="Dockerfile_alpine3.12"
        ;;
    ubuntu | ubuntu20.04)
        DockerFile="Dockerfile_ubuntu20.04"
        ;;
    *)
        print_usage
        ;;
    esac
    echo "$DockerFile"
}
