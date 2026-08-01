#!/bin/bash
DIR_0="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
dotnet publish -r linux-arm64 -p:PublishSingleFile=true -o ~/publish/util ${DIR_0}
rm -f ~/publish/util/*.json
rsync -avzF ~/publish/util/ cf.kra:/opt/docker/app/core.webutil/app/r/
