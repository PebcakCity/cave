# Cave

## Controllers for AV Equipment

A work in progress rewritten from my similar project in python, which has been [moved](https://github.com/PebcakCity/cavepy)

Gradually rewriting controller classes and reorganized everything into 3 projects:

> Cave - Device controller library.  Currently there is still only a 
> controller for NEC projectors.  I plan to implement PJLink support
> at some point, when I'm finally satisfied with the whole project
> layout and decide to stop screwing with it.  Then gradually other
> devices... TVs, switchers, etc.

> CaveBlazor - A test Blazor server app using the library.  The idea is
> to have one component be a collection of devices (DeviceCollection/Room)
> with config data loaded from a file/database/whatever and have it 
> instantiate a separate DeviceController component/tab for each device 
> it finds.  DeviceController displays different controls for different
> device types.  

> CaveGtk - GtkSharp app using the library.  Mainly just for
> testing/troubleshooting.  
