# Architecture

The legacy solution and GUI remain available while the portable solution grows.
SDK projects link selected original sources so a type has one owner in each build.
`GARbro.Core` initially owns ArcView, ArcViewStream and binary I/O. Images and
application services will be added after their dependencies have portable models.
The core must never reference WPF, Avalonia, Windows Forms or a playback device.
