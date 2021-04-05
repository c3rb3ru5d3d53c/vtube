# Unity VTube Studio

__A Cross Platform Unity Project for VTubers__

![version](https://img.shields.io/badge/Platform-Win-green)
![version](https://img.shields.io/badge/Platform-Lin-green)
![version](https://img.shields.io/badge/Platform-Mac-green)

__About the Project:__

This is my VTube Studio Unity Project, it works on Linux, Windows and MacOS using `Unity` and `OpenSeeFace`.

Since I'm a Linux user, I tried `VSeeFace` using wine. However, this was buggy and really needed a native solution.

I came across emilianavt's `OpenSeeFaceSample` project, and started working on my own modification of it.

I will keep updating this project to make things more Linux friendly but still be operational for Windows, and MacOS.

If you want to see videos I make using this project, visit my [YouTube Channel](https://www.youtube.com/channel/UCASUdhRodl9IBbL3hB-L1Jw).

# Installing

To use this project you must first download `UnityHub`.
```
wget https://public-cdn.cloud.unity3d.com/hub/prod/UnityHub.AppImage -O unityhub
chmod +x unityhub && mv unityhub /usr/bin
```

After `UnityHub` is downloaded, you will need to clone this repository.

```
git clone --recursive https://github.com/c3rb3ru5d3d53c/vtube.git
```

Once all this is completed, you can then launch `UnityHub` and open the project.

# Face Tracking

In order to support face tracking, you have to setup `OpenSeeFace`.

```bash
virtualenv -p python3 venv
source venv/bin/activate
pip3 install onnxruntime==1.2.0 opencv-python pillow numpy
python3 Tools/OpenSeeFace/facetracker.py -c 0 -W 1280 -H 720 --discard-after 0 --scan-every 0 --no-3d-adapt 1 --max-feature-updates 900
```

# Screen Sharing

To share you screen on a plane in the a scene, create a plane and add the the `Assets/Scripts/VideoDevice.cs` script as a component to the object.

Then you must drag and drop the `Assets/Scripts/VideoDevice.mat` on the plane.

When this is done, you have to do the following:

```bash
sudo apt install -y v4l2loopback-dkim ffmpeg
sudo modprobe v4l2loopback
Tools/screenshare.sh <monitor-number> <dummy-video-device>
```

Once `screenshare.sh` is running, then using the `VideoDevice.cs` script, you must set the `deviceName` to something like `/dev/video2` where the dummy `v4l2loopback` device was created.

It will then be possible to click `Play` in `Unity` and the screen sharing should be visible.

Interestingly, Unity does not support `yuv` format but it does support `yuyv` format.
