# Photography

`CMUPhotoCamera` ("photo camera", the travel camera sprite) prints real pictures. The journalist carries one in
their backpack; spawn it with the admin spawn menu to try it.

- Hold it in the active hand and click a tile up to 10 tiles away. The print of the square around that tile comes
  out into your hand, or at your feet when your hands are full.
- Alt click the camera to cycle the frame between 1x1, 3x3, 5x5 and 7x7. While it is held, an outline under the
  mouse shows the frame, red when out of range.
- Examine a print to see what it shows: people by their visible identity and condition (dead, badly hurt), then
  objects grouped by name. Walls, floors and anything the photographer could not see are left out.
- Use a print to open it full size, with the same list as a caption.

How it works: the click makes the server ask the photographer's client for the picture. The client cuts the
frame out of its own game view (`ScalingViewport.Screenshot`, then whitelisted ImageSharp `Crop` and `Resize`;
the sandbox forbids the pixel indexer) and sends a PNG at 32 px per tile. The server only accepts it for a request
it made, within 10 seconds, at exactly that size and at most 160 KiB, keeps it for the round (400 prints) and
sends it to clients that ask by print id. Every print is admin logged with its photographer. A modified client can
still send any image of the right size; the admin log names who took it.

Code: `Content.CMU/{Shared,Server,Client}/Photography/`. Prototypes: `Resources/Prototypes/CMU14/Photography/`.
Test: `IntegrationTests/Photography/CMUPhotoTest.cs`.
