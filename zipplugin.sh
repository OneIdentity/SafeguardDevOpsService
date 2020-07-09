#!/bin/bash

if [[ $# -ne 2 ]]; then
    >&2 echo "Usage: zipplugin.sh <sourcedir> <zipfilename>"
    exit 1
fi

scriptdir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
sourcedir=$1
zipfilename=$2

echo "scriptdir=$scriptdir"
echo "sourcedir=$sourcedir"
echo "zipfilename=$zipfilename"

if [ -z "$(which zip)" ]; then
    >&2 echo "This script requires the zip utility for packaging external plugins"
    exit 1
fi

if [ -f "$zipfilename" ]; then
    rm -f $zipfilename
fi

# cp $scriptdir/Manifest.json $sourcedir
echo "missing zip command"
#Add-Type -Assembly System.IO.Compression.FileSystem
#$compressionLevel = [System.IO.Compression.CompressionLevel]::Optimal
#[System.IO.Compression.ZipFile]::CreateFromDirectory($sourcedir, $zipfilename, $compressionLevel, $false)