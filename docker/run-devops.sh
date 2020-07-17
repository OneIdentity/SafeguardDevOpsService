#!/bin/bash

ScriptDir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Main script
if test -t 1; then
    YELLOW='\033[1;33m'
    NC='\033[0m'
fi

cleanup()
{
    if [ ! -z "$(jobs -p)" ]; then
        jobs -p | xargs kill
    fi
}
trap cleanup EXIT

echo -e "${YELLOW}Starting SafeguardDevOpsService${NC}"
bash -c 'while true; do echo -e "SafeguardDevOpsService is not running, executing..."; /home/safeguard/SafeguardDevOpsService; sleep 1; done' &

echo -e "${YELLOW}Sleeping to give SafeguardDevOpsService time to start${NC}"
sleep 3

echo -e "${YELLOW}Showing SafeguardDevOpsService logs${NC}"
tail -f -n +1 /usr/share/SafeguardDevOpsService/SafeguardDevOpsService.log
