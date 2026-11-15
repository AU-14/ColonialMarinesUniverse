// Paste the contents into scsi. Do not use #load on this engine build.
using System;
{
    var viewerName = "CMU_client1"; // Match the showcase script.
    (AppDomain.CurrentDomain.GetData("CMU.BracerShowcase." + viewerName) as Action)?.Invoke();
    write("Bracer showcase stopped. Its stage was removed and the previous body restored where available.");
}
