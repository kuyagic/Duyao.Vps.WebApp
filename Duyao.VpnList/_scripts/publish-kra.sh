#!/bin/bash
set -x
publish_dir=~/publish/vpn
rm -r "${publish_dir}/*"
dotnet publish -r linux-arm64 -p:PublishSingleFile=false -p:PublishCombined=false \
-o "${publish_dir}/" \
~/\@proj/Duyao.Vps.WebApp/Duyao.VpnList/

rm "${publish_dir}/appsettings.json"
rsync -avzF "${publish_dir}/" cf.kra:/opt/docker/app/core.vpn/app/r/

