#!/bin/sh

# Copy container-owned assets (eco-icons, lang) into the volume-mounted directory.
# This ensures updates to these files in the image are reflected even when
# the volume already contains older versions from a previous run.
cp -r /app/assets-seed/eco-icons /app/wwwroot/assets/
cp -r /app/assets-seed/lang /app/wwwroot/assets/

exec dotnet ecocraft.dll
