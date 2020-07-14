#!/bin/bash

if [[ $# -ne 3 ]]; then
    >&2 echo "Usage: zipplugin.sh <projectdir> <builddir> <zipfilename>"
    exit 1
fi

scriptdir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
projectdir=$1
builddir=$2
zipfilename=$3

echo "scriptdir=$scriptdir"
echo "projectdir=$projectdir"
echo "builddir=$builddir"
echo "zipfilename=$zipfilename"

if [ -z "$(which zip)" ]; then
    >&2 echo "This script requires the zip utility for packaging external plugins"
    exit 1
fi

if [ -f "$zipfilename" ]; then
    rm -f $zipfilename
fi

cp $projectdir/Manifest.json $builddir
cd $builddir && zip -r $zipfilename . -x netcoreapp2.2/ -x netcoreapp2.2/* -x netcoreapp2/publish/ -x netcoreapp2.2/publish/*