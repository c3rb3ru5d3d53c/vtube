#!/usr/bin/env bash

# Requirements:
# sudo apt install -y v4l2loopback-dkms ffmpeg
# sudo nano /etc/modules and add the line v4l2loopback

if [ "$#" -lt 2 ]; then
    echo "Usage: ./screenshare.sh <monitor-number> <video-device>"
    exit 1
fi

CLIP=$(xrandr --listactivemonitors | awk -- 'BEGIN { getline } { gsub(/\/[[:digit:]]+/,"",$3) ; print $3 }' | sed -n -e $1p | head -1)
RESOLUTION=$(echo $CLIP | grep -Po '^\d+x\d+')
OFFSET=$(echo $CLIP | grep -Po '\+\d+\+\d+' | sed 's/^.//;s/\+/,/;')

ffmpeg -f x11grab -r 15 -s $RESOLUTION -i ":0.0+${OFFSET}" -vcodec rawvideo -pix_fmt yuyv422 -threads 0 -f v4l2 $2
