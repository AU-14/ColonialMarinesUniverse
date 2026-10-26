# Parse SWSL and generate GLSL with the existing built Robust.Client assembly.
# No project builds, engine modifications, game startup, or window creation.
param(
    [string] $Root = (Join-Path $PSScriptRoot '../..'),
    [string] $Shader = 'Content.CMU/Resources/Textures/CMU14/Interface/three_d_scene.swsl',
    [switch] $Benchmark
)
$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path -LiteralPath $Root).Path
$assemblyDirectory = Join-Path $Root 'bin/Content.Client'
if (!(Test-Path -LiteralPath (Join-Path $assemblyDirectory 'Robust.Client.dll'))) {
    throw 'Build Content.Client before running this validation. This script does not build projects.'
}

# Load bytes to avoid locking shared build outputs. Dependencies are resolved from that same build.
$resolver = [System.ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = [System.Reflection.AssemblyName]::new($eventArgs.Name).Name
    $path = Join-Path $assemblyDirectory ($name + '.dll')
    if (Test-Path -LiteralPath $path) {
        return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($path))
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $assembly = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Join-Path $assemblyDirectory 'Robust.Client.dll')))
    $parser = $assembly.GetType('Robust.Client.Graphics.ShaderParser', $true)
    $parse = $parser.GetMethods() | Where-Object { $_.Name -eq 'Parse' -and $_.GetParameters()[0].ParameterType -eq [string] }
    $sourcePath = Join-Path $Root $Shader
    $source = [System.IO.File]::ReadAllText($sourcePath)
    # This shader has no includes, so parsing does not need an initialized resource manager.
    $parsed = $parse.Invoke($null, @($source, $null))

    $flags = [System.Reflection.BindingFlags]'Instance, NonPublic'
    $clydeType = $assembly.GetType('Robust.Client.Graphics.Clyde.Clyde', $true)
    $clyde = [System.Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject($clydeType)
    $shaderDirectory = Join-Path $Root 'RobustToolbox/Robust.Client/Graphics/Clyde/Shaders'
    foreach ($pair in @(
        @('_shaderWrapCodeDefaultVert', 'base-default.vert'),
        @('_shaderWrapCodeDefaultFrag', 'base-default.frag'),
        @('_shaderWrapCodeRawVert', 'base-raw.vert'),
        @('_shaderWrapCodeRawFrag', 'base-raw.frag')
    )) {
        $field = $clydeType.GetField($pair[0], $flags)
        $field.SetValue($clyde, [System.IO.File]::ReadAllText((Join-Path $shaderDirectory $pair[1])))
    }
    # GetShaderCode is pure string generation. Do not invoke Clyde initialization or GL APIs here.
    $code = $clydeType.GetMethod('GetShaderCode', $flags).Invoke($clyde, @($parsed))

    # Exercise the actual content encoder and picker from the same built client, without IoC/UI.
    $content = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Join-Path $assemblyDirectory 'Content.Client.dll')))
    $boxType = $content.GetType('Content.Client.CMU14.ThreeD.Scene.CMU3DSceneBox', $true)
    $encodingType = $content.GetType('Content.Client.CMU14.ThreeD.Scene.CMU3DSceneEncoding', $true)
    $constructor = $boxType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 10 }
    $boxShape = [Enum]::ToObject($constructor.GetParameters()[5].ParameterType, 0)
    $ellipsoidShape = [Enum]::ToObject($constructor.GetParameters()[5].ParameterType, 1)
    $surfaceAxis = [Enum]::ToObject($constructor.GetParameters()[7].ParameterType, 1)
    $colorConstructor = $constructor.GetParameters()[3].ParameterType.GetConstructor(@([float], [float], [float], [float]))
    $blue = $colorConstructor.Invoke(@([float]0, [float]0, [float]1, [float]1))
    $red = $colorConstructor.Invoke(@([float]1, [float]0, [float]0, [float]1))
    $boxes = [Array]::CreateInstance($boxType, 2)
    $boxes.SetValue($constructor.Invoke(@([System.Numerics.Vector3]::new(0, 0.7, 1.3),
        [System.Numerics.Vector3]::new(1.15, 0.2, 1.05), [float]-0.2, $blue, $null, $boxShape, [ushort]0, $surfaceAxis, [float]1, $false)), 0)
    $boxes.SetValue($constructor.Invoke(@([System.Numerics.Vector3]::new(0, -0.8, 1.3),
        [System.Numerics.Vector3]::new(0.45, 0.3, 0.7), [float]0.45, $red, $null, $ellipsoidShape, [ushort]0, $surfaceAxis, [float]1, $false)), 1)
    $encoding = [Activator]::CreateInstance($encodingType)
    $encodingType.GetMethod('Build').Invoke($encoding, @(,$boxes)) | Out-Null
    if ($encoding.AcceptedBoxes -ne 2 -or $encoding.OmittedBoxes -ne 0) {
        throw 'The native smoke scene was unexpectedly rejected by the actual encoder.'
    }
    function PackedPixels($pixels) {
        $bytes = [byte[]]::new($pixels.Length * 4)
        for ($i = 0; $i -lt $pixels.Length; $i++) {
            $bytes[$i * 4] = $pixels[$i].R
            $bytes[$i * 4 + 1] = $pixels[$i].G
            $bytes[$i * 4 + 2] = $pixels[$i].B
            $bytes[$i * 4 + 3] = $pixels[$i].A
        }
        return [Convert]::ToBase64String($bytes)
    }
    $origin = [System.Numerics.Vector3]::new(0, -5, 1.3)
    $frameType = $content.GetType('Content.Client.CMU14.ThreeD.CMU3DCameraFrame', $true)
    $frame = $frameType.GetConstructors()[0].Invoke(@($origin, [System.Numerics.Vector3]::UnitY,
        [System.Numerics.Vector3]::UnitX, [System.Numerics.Vector3]::UnitZ,
        [float](32.5 / [Math]::Tan([Math]::PI / 8)), [System.Numerics.Vector2]::new(32.5, 32.5)))
    $samples = @()
    foreach ($sample in @(@('near-red', 32, 32, 1), @('far-blue', 46, 32, 0), @('curved-corner-gap', 39, 43, 0), @('empty', 2, 62, -1))) {
        # GL readback is bottom-up; the public camera helper takes top-left UI pixels.
        $pixel = [System.Numerics.Vector2]::new($sample[1] + 0.5, 65 - ($sample[2] + 0.5))
        $ray = $frameType.GetMethod('RayDirection').Invoke($frame, @($pixel))
        $arguments = @($origin, $ray, $null, $null)
        $found = $encodingType.GetMethod('TryPick').Invoke($encoding, $arguments)
        $index = if ($found) { $arguments[2].BoxIndex } else { -1 }
        if ($index -ne $sample[3]) { throw "Unexpected CPU picking result for $($sample[0]): $index" }
        $samples += @{ name = $sample[0]; pixel = @($sample[1], $sample[2]); cpuBoxIndex = $index }
    }
    # Asymmetric source pixels exercise atlas row addressing, cutouts and room-side mirroring.
    $surfaceType = $content.GetType('Content.Client.CMU14.ThreeD.Scene.CMU3DSceneSurfaces', $true)
    $surfaces = [Activator]::CreateInstance($surfaceType)
    $pixelType = $surfaceType.GetMethod('Add').GetParameters()[3].ParameterType.GetElementType()
    $pixelConstructor = $pixelType.GetConstructor(@([byte], [byte], [byte], [byte]))
    $pixels = [Array]::CreateInstance($pixelType, 4)
    $palette = @(@(255,0,0,255), @(0,255,0,255), @(0,0,255,255), @(200,90,40,0))
    for ($i = 0; $i -lt 4; $i++) {
        $pixels.SetValue($pixelConstructor.Invoke(@([byte]$palette[$i][0], [byte]$palette[$i][1], [byte]$palette[$i][2], [byte]$palette[$i][3])), $i)
    }
    foreach ($slot in @(17, 256, 4095)) {
        $surfaceType.GetMethod('Add').Invoke($surfaces, @([ushort]$slot, 2, 2, $pixels)) | Out-Null
    }
    $white = $colorConstructor.Invoke(@([float]1, [float]1, [float]1, [float]1))
    $yellow = $colorConstructor.Invoke(@([float]1, [float]1, [float]0, [float]1))
    $surfaceSmokes = @()
    $wedgeShape = [Enum]::ToObject($constructor.GetParameters()[5].ParameterType, 5)
    $reverseWedgeShape = [Enum]::ToObject($constructor.GetParameters()[5].ParameterType, 6)
    foreach ($surfaceShape in @($boxShape, $wedgeShape, $reverseWedgeShape)) {
    foreach ($slot in @(17, 256, 4095)) {
    foreach ($flip in @($false, $true)) {
      foreach ($vScale in @([float]1, [float]0.5)) {
        $panelBoxes = [Array]::CreateInstance($boxType, 2)
        $panelBoxes.SetValue($constructor.Invoke(@([System.Numerics.Vector3]::new(0, .5, 1.3),
            [System.Numerics.Vector3]::new(1.2, .05, 1.2), [float]0, $yellow, $null, $boxShape, [ushort]0, $surfaceAxis, [float]1, $false)), 0)
        $panelBoxes.SetValue($constructor.Invoke(@([System.Numerics.Vector3]::new(0, 0, 1.3),
            [System.Numerics.Vector3]::new(.8, .05, .8), [float]0, $white, $null, $surfaceShape, [ushort]$slot, $surfaceAxis, $vScale, $flip)), 1)
        $panelEncoding = [Activator]::CreateInstance($encodingType)
        $encodingType.GetMethod('Build').Invoke($panelEncoding, @(,$panelBoxes)) | Out-Null
        $panelSamples = @()
        foreach ($sample in @(@('upper-left',25,39,0), @('upper-right',39,39,1), @('lower-left',25,25,2), @('cutout',39,25,3))) {
            $sourceCorner = if ($flip) { $sample[3] -bxor 1 } else { $sample[3] }
            if ($vScale -lt 1) { $sourceCorner = $sourceCorner -bor 2 }
            $expectedIndex = if ($sourceCorner -eq 3) { 0 } else { 1 }
            $expectedColor = @(@(255,0,0), @(0,255,0), @(0,0,255), @(255,255,0))[$sourceCorner]
            $pixel = [System.Numerics.Vector2]::new($sample[1]+.5,65-($sample[2]+.5))
            $ray = $frameType.GetMethod('RayDirection').Invoke($frame,@($pixel))
            $arguments = @($origin,$ray,$null,$surfaces)
            $found = $encodingType.GetMethod('TryPick').Invoke($panelEncoding,$arguments)
            $index = if ($found) { $arguments[2].BoxIndex } else { -1 }
            if ($index -ne $expectedIndex) { throw "Surface CPU picking mismatch: $($sample[0]), flip $flip" }
            $panelSamples += @{ name=$sample[0]; pixel=@($sample[1],$sample[2]); cpuBoxIndex=$index; expectedRGB=$expectedColor }
        }
        $surfaceSmokes += @{
            name=$surfaceShape.ToString(); width=65; height=65; flipU=$flip; vScale=$vScale; surfaceFixture=$true; surfaceSlot=$slot
            surfaceWidth=$surfaces.Width; surfaceHeight=$surfaces.Height
            surfacePixels=PackedPixels $surfaces.AtlasPixels(); surfaceInformation=PackedPixels $surfaces.Information
            boxRows=$encodingType.GetField('BoxTextureHeight').GetRawConstantValue()
            origin=@(0,-5,1.3); forward=@(0,1,0); right=@(1,0,0); up=@(0,0,1)
            sceneZ=@($panelEncoding.MinZ,$panelEncoding.MaxZ)
            boxPixels=PackedPixels $panelEncoding.BoxPixels; gridPixels=PackedPixels $panelEncoding.GridPixels
            samples=$panelSamples
        }
      }
    }
    }
    }
    $wedgeSmokes = @()
    foreach ($testedWedge in @($wedgeShape, $reverseWedgeShape)) {
    foreach ($yaw in @([float]0,[float]0.63)) {
        $rotation=[System.Numerics.Matrix4x4]::CreateRotationZ($yaw)
        $forward=[System.Numerics.Vector3]::TransformNormal(-[System.Numerics.Vector3]::UnitX,$rotation)
        $right=[System.Numerics.Vector3]::TransformNormal([System.Numerics.Vector3]::UnitY,$rotation)
        $center=[System.Numerics.Vector3]::new(0,0,1.3);$wedgeOrigin=$center-$forward*5
        $wedgeBoxes=[Array]::CreateInstance($boxType,2)
        $wedgeBoxes.SetValue($constructor.Invoke(@(($center+$forward*1.5),[System.Numerics.Vector3]::new(1.2),[float]0,$blue,$null,$boxShape,[ushort]0,$surfaceAxis,[float]1,$false)),0)
        $wedgeBoxes.SetValue($constructor.Invoke(@($center,[System.Numerics.Vector3]::new(.6),$yaw,$red,$null,$testedWedge,[ushort]0,$surfaceAxis,[float]1,$false)),1)
        $wedgeEncoding=[Activator]::CreateInstance($encodingType)
        $encodingType.GetMethod('Build').Invoke($wedgeEncoding,@(,$wedgeBoxes))|Out-Null
        if($wedgeEncoding.AcceptedBoxes -ne 2){throw 'Wedge fixture rejected by encoder.'}
        $wedgeFrame=$frameType.GetConstructors()[0].Invoke(@($wedgeOrigin,$forward,$right,[System.Numerics.Vector3]::UnitZ,
            [float](32.5/[Math]::Tan([Math]::PI/8)),[System.Numerics.Vector2]::new(32.5,32.5)))
        $wedgeSamples=@()
        foreach($sample in @(@('lower-back-solid',38,26,1),@('upper-front-gap',26,38,0),@('high-back-solid',39,37,1),@('empty',2,62,-1))){
            if ($testedWedge -eq $reverseWedgeShape) { $sample[1] = 64 - $sample[1] }
            $ray=$frameType.GetMethod('RayDirection').Invoke($wedgeFrame,@([System.Numerics.Vector2]::new($sample[1]+.5,65-($sample[2]+.5))))
            $arguments=@($wedgeOrigin,$ray,$null,$null)
            $found=$encodingType.GetMethod('TryPick').Invoke($wedgeEncoding,$arguments)
            $index=if($found){$arguments[2].BoxIndex}else{-1}
            if($index -ne $sample[3]){throw "Wedge CPU mismatch $yaw $($sample[0]): $index"}
            $wedgeSamples+=@{name=$sample[0];pixel=@($sample[1],$sample[2]);cpuBoxIndex=$index}
        }
        $wedgeSmokes+=@{name="$testedWedge/$yaw";width=65;height=65;boxRows=$encodingType.GetField('BoxTextureHeight').GetRawConstantValue()
            origin=@($wedgeOrigin.X,$wedgeOrigin.Y,$wedgeOrigin.Z);forward=@($forward.X,$forward.Y,$forward.Z);right=@($right.X,$right.Y,$right.Z);up=@(0,0,1)
            sceneZ=@($wedgeEncoding.MinZ,$wedgeEncoding.MaxZ);boxPixels=PackedPixels $wedgeEncoding.BoxPixels;gridPixels=PackedPixels $wedgeEncoding.GridPixels;samples=$wedgeSamples}
    }
    }
    # Continuous leaves occupy their leaning diagonal, leaving opposite corners empty.
    $slantedSmokes = @()
    foreach ($shapeCode in 7..10) {
    foreach ($yaw in @([float]0,[float]0.63)) {
        $testedShape=[Enum]::ToObject($constructor.GetParameters()[5].ParameterType,$shapeCode)
        $rotation=[System.Numerics.Matrix4x4]::CreateRotationZ($yaw)
        $alongX=$shapeCode -lt 9
        $localForward=if($alongX){[System.Numerics.Vector3]::UnitY}else{-[System.Numerics.Vector3]::UnitX}
        $localRight=if($alongX){[System.Numerics.Vector3]::UnitX}else{[System.Numerics.Vector3]::UnitY}
        $half=if($alongX){[System.Numerics.Vector3]::new(.6,.2,.6)}else{[System.Numerics.Vector3]::new(.2,.6,.6)}
        $forward=[System.Numerics.Vector3]::TransformNormal($localForward,$rotation)
        $right=[System.Numerics.Vector3]::TransformNormal($localRight,$rotation)
        $center=[System.Numerics.Vector3]::new(0,0,1.3);$leafOrigin=$center-$forward*5
        $leafBoxes=[Array]::CreateInstance($boxType,2)
        $leafBoxes.SetValue($constructor.Invoke(@(($center+$forward*1.5),[System.Numerics.Vector3]::new(1.2),[float]0,$blue,$null,$boxShape,[ushort]0,$surfaceAxis,[float]1,$false)),0)
        $leafBoxes.SetValue($constructor.Invoke(@($center,$half,$yaw,$red,$null,$testedShape,[ushort]0,$surfaceAxis,[float]1,$false)),1)
        $leafEncoding=[Activator]::CreateInstance($encodingType)
        $encodingType.GetMethod('Build').Invoke($leafEncoding,@(,$leafBoxes))|Out-Null
        if($leafEncoding.AcceptedBoxes -ne 2){throw 'Slanted fixture rejected by encoder.'}
        $leafFrame=$frameType.GetConstructors()[0].Invoke(@($leafOrigin,$forward,$right,[System.Numerics.Vector3]::UnitZ,
            [float](32.5/[Math]::Tan([Math]::PI/8)),[System.Numerics.Vector2]::new(32.5,32.5)))
        $leafSamples=@()
        foreach($sample in @(@('diagonal-blade',38,38,1),@('opposite-corner-gap',26,38,0),@('center',32,32,1),@('empty',2,62,-1))){
            if (($shapeCode % 2) -eq 0) { $sample[1] = 64 - $sample[1] }
            $ray=$frameType.GetMethod('RayDirection').Invoke($leafFrame,@([System.Numerics.Vector2]::new($sample[1]+.5,65-($sample[2]+.5))))
            $arguments=@($leafOrigin,$ray,$null,$null)
            $found=$encodingType.GetMethod('TryPick').Invoke($leafEncoding,$arguments)
            $index=if($found){$arguments[2].BoxIndex}else{-1}
            if($index -ne $sample[3]){throw "Slanted CPU mismatch $testedShape/$yaw $($sample[0]): $index"}
            $leafSamples+=@{name=$sample[0];pixel=@($sample[1],$sample[2]);cpuBoxIndex=$index}
        }
        $slantedSmokes+=@{name="$testedShape/$yaw";width=65;height=65;boxRows=$encodingType.GetField('BoxTextureHeight').GetRawConstantValue()
            origin=@($leafOrigin.X,$leafOrigin.Y,$leafOrigin.Z);forward=@($forward.X,$forward.Y,$forward.Z);right=@($right.X,$right.Y,$right.Z);up=@(0,0,1)
            sceneZ=@($leafEncoding.MinZ,$leafEncoding.MaxZ);boxPixels=PackedPixels $leafEncoding.BoxPixels;gridPixels=PackedPixels $leafEncoding.GridPixels;samples=$leafSamples}
    }
    }
    # Dense samples compare native GPU leaf gaps against the exact CPU solid after packing.
    $foliageSmokes = @()
    foreach ($cameraYaw in @([float]0,[float]1.57,[float]3.14)) {
        $testedShape=[Enum]::ToObject($constructor.GetParameters()[5].ParameterType,11)
        $rotation=[System.Numerics.Matrix4x4]::CreateRotationZ($cameraYaw)
        $forward=[System.Numerics.Vector3]::TransformNormal([System.Numerics.Vector3]::UnitY,$rotation)
        $right=[System.Numerics.Vector3]::TransformNormal([System.Numerics.Vector3]::UnitX,$rotation)
        $center=[System.Numerics.Vector3]::new(0,0,1.3);$leafOrigin=$center-$forward*3
        $leafBoxes=[Array]::CreateInstance($boxType,2)
        $leafBoxes.SetValue($constructor.Invoke(@(($center+$forward*1.7),[System.Numerics.Vector3]::new(1.2),[float]0,$blue,$null,$boxShape,[ushort]0,$surfaceAxis,[float]1,$false)),0)
        $leafBoxes.SetValue($constructor.Invoke(@($center,[System.Numerics.Vector3]::new(.6,.4,.65),[float]0.63,$red,$null,$testedShape,[ushort]0,$surfaceAxis,[float]1,$false)),1)
        $leafEncoding=[Activator]::CreateInstance($encodingType)
        $encodingType.GetMethod('Build').Invoke($leafEncoding,@(,$leafBoxes))|Out-Null
        if($leafEncoding.AcceptedBoxes -ne 2){throw 'Foliage fixture rejected by encoder.'}
        $leafFrame=$frameType.GetConstructors()[0].Invoke(@($leafOrigin,$forward,$right,[System.Numerics.Vector3]::UnitZ,
            [float](32.5/[Math]::Tan([Math]::PI/8)),[System.Numerics.Vector2]::new(32.5,32.5)))
        $leafSamples=@();$leafHits=0;$leafGaps=0
        for($y=20;$y -le 44;$y+=2){for($x=20;$x -le 44;$x+=2){
            $ray=$frameType.GetMethod('RayDirection').Invoke($leafFrame,@([System.Numerics.Vector2]::new($x+.5,65-($y+.5))))
            $arguments=@($leafOrigin,$ray,$null,$null)
            $found=$encodingType.GetMethod('TryPick').Invoke($leafEncoding,$arguments)
            $index=if($found){$arguments[2].BoxIndex}else{-1}
            if($index -eq 1){$leafHits++}elseif($index -eq 0){$leafGaps++}
            $leafSamples+=@{name="foliage-$x-$y";pixel=@($x,$y);cpuBoxIndex=$index}
        }}
        if($leafHits -lt 10 -or $leafGaps -lt 10){throw "Foliage fixture does not exercise both leaves and gaps: $leafHits / $leafGaps"}
        $foliageSmokes+=@{name="Foliage/$cameraYaw";width=65;height=65;boxRows=$encodingType.GetField('BoxTextureHeight').GetRawConstantValue()
            origin=@($leafOrigin.X,$leafOrigin.Y,$leafOrigin.Z);forward=@($forward.X,$forward.Y,$forward.Z);right=@($right.X,$right.Y,$right.Z);up=@(0,0,1)
            sceneZ=@($leafEncoding.MinZ,$leafEncoding.MaxZ);boxPixels=PackedPixels $leafEncoding.BoxPixels;gridPixels=PackedPixels $leafEncoding.GridPixels;samples=$leafSamples}
    }
    # Dense samples compare native GPU leaf gaps against the exact CPU solid after packing.
    $tiltSmokes = @()
    foreach ($tiltShape in @(0,1,2,11)) {
    foreach ($tiltAngle in @([float]0.6,[float]-1.2)) {
    foreach ($cameraYaw in @([float]0,[float]1.57)) {
        $testedShape=[Enum]::ToObject($constructor.GetParameters()[5].ParameterType,$tiltShape)
        $rotation=[System.Numerics.Matrix4x4]::CreateRotationZ($cameraYaw)
        $forward=[System.Numerics.Vector3]::TransformNormal([System.Numerics.Vector3]::UnitY,$rotation)
        $right=[System.Numerics.Vector3]::TransformNormal([System.Numerics.Vector3]::UnitX,$rotation)
        $center=[System.Numerics.Vector3]::new(0,0,1.3);$leafOrigin=$center-$forward*3
        $leafBoxes=[Array]::CreateInstance($boxType,2)
        $leafBoxes.SetValue($constructor.Invoke(@(($center+$forward*1.7),[System.Numerics.Vector3]::new(1.2),[float]0,$blue,$null,$boxShape,[ushort]0,$surfaceAxis,[float]1,$false)),0)
        $tilted=$constructor.Invoke(@($center,[System.Numerics.Vector3]::new(.8,.15,.15),[float]0.63,$red,$null,$testedShape,[ushort]0,$surfaceAxis,[float]1,$false))
        $boxType.GetProperty('Pitch').SetValue($tilted,$tiltAngle)
        $leafBoxes.SetValue($tilted,1)
        $leafEncoding=[Activator]::CreateInstance($encodingType)
        $encodingType.GetMethod('Build').Invoke($leafEncoding,@(,$leafBoxes))|Out-Null
        if($leafEncoding.AcceptedBoxes -ne 2){throw 'Tilt fixture rejected by encoder.'}
        $leafFrame=$frameType.GetConstructors()[0].Invoke(@($leafOrigin,$forward,$right,[System.Numerics.Vector3]::UnitZ,
            [float](32.5/[Math]::Tan([Math]::PI/8)),[System.Numerics.Vector2]::new(32.5,32.5)))
        $leafSamples=@();$leafHits=0;$leafGaps=0
        for($y=20;$y -le 44;$y+=2){for($x=20;$x -le 44;$x+=2){
            $ray=$frameType.GetMethod('RayDirection').Invoke($leafFrame,@([System.Numerics.Vector2]::new($x+.5,65-($y+.5))))
            $arguments=@($leafOrigin,$ray,$null,$null)
            $found=$encodingType.GetMethod('TryPick').Invoke($leafEncoding,$arguments)
            $index=if($found){$arguments[2].BoxIndex}else{-1}
            if($index -eq 1){$leafHits++}elseif($index -eq 0){$leafGaps++}
            $leafSamples+=@{name="tilt-$x-$y";pixel=@($x,$y);cpuBoxIndex=$index}
        }}
        if($leafHits -lt 1 -or $leafGaps -lt 10){throw "Tilt fixture does not exercise both leaves and gaps: $leafHits / $leafGaps"}
        $tiltSmokes+=@{name="Tilt/$tiltShape/$tiltAngle/$cameraYaw";width=65;height=65;boxRows=$encodingType.GetField('BoxTextureHeight').GetRawConstantValue()
            origin=@($leafOrigin.X,$leafOrigin.Y,$leafOrigin.Z);forward=@($forward.X,$forward.Y,$forward.Z);right=@($right.X,$right.Y,$right.Z);up=@(0,0,1)
            sceneZ=@($leafEncoding.MinZ,$leafEncoding.MaxZ);boxPixels=PackedPixels $leafEncoding.BoxPixels;gridPixels=PackedPixels $leafEncoding.GridPixels;samples=$leafSamples}
    }
    }
    }
    # Fractional source and paint alpha must reveal the opaque panel beneath,
    # while picking remains continuous for retained pixels (not checkerboard holes).
    $alphaSmokes = @()
    $pattern = @(0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5)
    foreach ($case in @(@(255, .5), @(128, 1.0), @(128, .5), @(255, 0.0), @(127, 1.0))) {
        $alphaSurfaces = [Activator]::CreateInstance($surfaceType)
        $alphaPixels = [Array]::CreateInstance($pixelType, 1)
        $alphaPixels.SetValue($pixelConstructor.Invoke(@([byte]255,[byte]0,[byte]0,[byte]$case[0])),0)
        $surfaceType.GetMethod('Add').Invoke($alphaSurfaces,@([ushort]1,1,1,$alphaPixels)) | Out-Null
        $paint = $colorConstructor.Invoke(@([float]1,[float]1,[float]1,[float]$case[1]))
        $alphaBoxes = [Array]::CreateInstance($boxType,2)
        $alphaBoxes.SetValue($constructor.Invoke(@([System.Numerics.Vector3]::new(0,.5,1.3),
            [System.Numerics.Vector3]::new(1.2,.05,1.2),[float]0,$yellow,$null,$boxShape,[ushort]0,$surfaceAxis,[float]1,$false)),0)
        $alphaBoxes.SetValue($constructor.Invoke(@([System.Numerics.Vector3]::new(0,0,1.3),
            [System.Numerics.Vector3]::new(.8,.05,.8),[float]0,$paint,$null,$boxShape,[ushort]1,$surfaceAxis,[float]1,$false)),1)
        $alphaEncoding = [Activator]::CreateInstance($encodingType)
        $encodingType.GetMethod('Build').Invoke($alphaEncoding,@(,$alphaBoxes)) | Out-Null
        if ($alphaEncoding.AcceptedBoxes -ne 2) { throw 'Alpha fixture rejected by encoder.' }
        $alphaSamples = @()
        foreach ($y in 28..31) { foreach ($x in 28..31) {
            $ray = $frameType.GetMethod('RayDirection').Invoke($frame,@([System.Numerics.Vector2]::new($x+.5,65-($y+.5))))
            $arguments = @($origin,$ray,$null,$alphaSurfaces)
            $found = $encodingType.GetMethod('TryPick').Invoke($alphaEncoding,$arguments)
            $index = if ($found) { $arguments[2].BoxIndex } else { -1 }
            $expectedIndex = if ($case[0] -ge 128 -and $case[1] -gt 0) { 1 } else { 0 }
            if ($index -ne $expectedIndex) { throw "Alpha picking mismatch for $case" }
            $coverage = $case[0] / 255.0 * $alphaEncoding.BoxPixels[10].A / 255.0
            $retained = $case[0] -ge 128 -and $coverage -ge (($pattern[($y % 4)*4 + ($x % 4)] + .5) / 16.0)
            $expectedColor = if ($retained) { @(255,0,0) } else { @(255,255,0) }
            $alphaSamples += @{ name="alpha-$x-$y";pixel=@($x,$y);cpuBoxIndex=$index;expectedRGB=$expectedColor }
        } }
        $alphaSmokes += @{
            name="source-$($case[0])/paint-$($case[1])";width=65;height=65
            surfaceWidth=$alphaSurfaces.Width;surfaceHeight=$alphaSurfaces.Height
            surfacePixels=PackedPixels $alphaSurfaces.AtlasPixels();surfaceInformation=PackedPixels $alphaSurfaces.Information
            boxRows=$encodingType.GetField('BoxTextureHeight').GetRawConstantValue()
            origin=@(0,-5,1.3);forward=@(0,1,0);right=@(1,0,0);up=@(0,0,1)
            sceneZ=@($alphaEncoding.MinZ,$alphaEncoding.MaxZ)
            boxPixels=PackedPixels $alphaEncoding.BoxPixels;gridPixels=PackedPixels $alphaEncoding.GridPixels
            samples=$alphaSamples
        }
    }
    # Each cylinder axis is viewed from both caps and from its straight side.
    $cylinderSmokes = @()
    foreach ($axis in 0..2) {
      foreach ($view in @('cap-front', 'cap-back', 'side')) {
        $cylinderShape = [Enum]::ToObject($constructor.GetParameters()[5].ParameterType, $axis + 2)
        $cameraAxis = if ($view -eq 'side') { ($axis + 1) % 3 } else { $axis }
        $basis = @([System.Numerics.Vector3]::UnitX, [System.Numerics.Vector3]::UnitY, [System.Numerics.Vector3]::UnitZ)
        $rotation = [System.Numerics.Matrix4x4]::CreateRotationZ([float]0.45)
        $forward = [System.Numerics.Vector3]::TransformNormal($basis[$cameraAxis], $rotation)
        if ($view -eq 'cap-back') { $forward = -$forward }
        $upBasis = if ($cameraAxis -eq 2) { [System.Numerics.Vector3]::UnitY } else { [System.Numerics.Vector3]::UnitZ }
        $upBasis = [System.Numerics.Vector3]::TransformNormal($upBasis, $rotation)
        $right = [System.Numerics.Vector3]::Normalize([System.Numerics.Vector3]::Cross($forward, $upBasis))
        $up = [System.Numerics.Vector3]::Cross($right, $forward)
        $center = [System.Numerics.Vector3]::new(0,0,1.3)
        $cylinderOrigin = $center - $forward * 5
        $cylinderBoxes = [Array]::CreateInstance($boxType, 2)
        $cylinderBoxes.SetValue($constructor.Invoke(@(($center + $forward * 1.5),
            [System.Numerics.Vector3]::new(1.1), [float]0, $blue, $null, $boxShape, [ushort]0, $surfaceAxis, [float]1, $false)), 0)
        $cylinderBoxes.SetValue($constructor.Invoke(@($center,
            [System.Numerics.Vector3]::new(.6), [float]0.45, $red, $null, $cylinderShape, [ushort]0, $surfaceAxis, [float]1, $false)), 1)
        $cylinderEncoding = [Activator]::CreateInstance($encodingType)
        $encodingType.GetMethod('Build').Invoke($cylinderEncoding, @(,$cylinderBoxes)) | Out-Null
        if ($cylinderEncoding.AcceptedBoxes -ne 2) { throw 'Cylinder fixture rejected by encoder.' }
        $cylinderFrame = $frameType.GetConstructors()[0].Invoke(@($cylinderOrigin, $forward, $right, $up,
            [float](32.5 / [Math]::Tan([Math]::PI / 8)), [System.Numerics.Vector2]::new(32.5,32.5)))
        $cornerExpected = if ($view -eq 'side') { 1 } else { 0 }
        $cylinderSamples = @()
        foreach ($sample in @(@('center',32,32,1), @('beyond-radius',46,32,0), @('cap-corner-or-straight-side',41,41,$cornerExpected), @('empty',2,62,-1))) {
            $pixel = [System.Numerics.Vector2]::new($sample[1]+.5,65-($sample[2]+.5))
            $ray = $frameType.GetMethod('RayDirection').Invoke($cylinderFrame,@($pixel))
            $arguments = @($cylinderOrigin,$ray,$null,$null)
            $found = $encodingType.GetMethod('TryPick').Invoke($cylinderEncoding,$arguments)
            $index = if ($found) { $arguments[2].BoxIndex } else { -1 }
            if ($index -ne $sample[3]) { throw "Cylinder CPU mismatch: axis $axis, $view, $($sample[0]): $index expected $($sample[3])" }
            $cylinderSamples += @{ name=$sample[0]; pixel=@($sample[1],$sample[2]); cpuBoxIndex=$index }
        }
        $cylinderSmokes += @{
            name="Cylinder$axis/$view"; width=65; height=65
            boxRows=$encodingType.GetField('BoxTextureHeight').GetRawConstantValue()
            origin=@($cylinderOrigin.X,$cylinderOrigin.Y,$cylinderOrigin.Z)
            forward=@($forward.X,$forward.Y,$forward.Z); right=@($right.X,$right.Y,$right.Z); up=@($up.X,$up.Y,$up.Z)
            sceneZ=@($cylinderEncoding.MinZ,$cylinderEncoding.MaxZ)
            boxPixels=PackedPixels $cylinderEncoding.BoxPixels; gridPixels=PackedPixels $cylinderEncoding.GridPixels
            samples=$cylinderSamples
        }
      }
    }
    # Probe both the old admission boundary and multi-byte counts. The nearest
    # panel is the final reference, including a full extended snapshot.
    $capacitySmokes = @()
    foreach ($cellCapacity in @(191,192,193,255,256,329,384,385,1025,8192,65536,$encodingType.GetField('ExtendedMaxBoxes').GetRawConstantValue())) {
        $denseBoxes = [Array]::CreateInstance($boxType, $cellCapacity)
        for ($i = 0; $i -lt $cellCapacity - 1; $i++) {
            $denseBoxes.SetValue($constructor.Invoke(@([System.Numerics.Vector3]::new(.5,.75,1.3),
                [System.Numerics.Vector3]::new(.4,.06,.4),[float]0,$blue,$null,$boxShape,[ushort]0,$surfaceAxis,[float]1,$false)), $i)
        }
        $denseBoxes.SetValue($constructor.Invoke(@([System.Numerics.Vector3]::new(.5,.25,1.3),
            [System.Numerics.Vector3]::new(.4,.06,.4),[float]0,$red,$null,$boxShape,[ushort]0,$surfaceAxis,[float]1,$false)), $cellCapacity-1)
        $denseEncoding = [Activator]::CreateInstance($encodingType, [object[]]@($cellCapacity -gt 8192))
        $encodingType.GetMethod('Build').Invoke($denseEncoding, @(,$denseBoxes)) | Out-Null
        if ($denseEncoding.AcceptedBoxes -ne $cellCapacity -or $denseEncoding.OmittedBoxes -ne 0) {
            throw "Dense cell fixture admission mismatch ($cellCapacity)."
        }
        $denseOrigin = [System.Numerics.Vector3]::new(.5,-2,1.3)
        $denseArgs = @($denseOrigin,[System.Numerics.Vector3]::UnitY,$null,$null)
        if (!$encodingType.GetMethod('TryPick').Invoke($denseEncoding,$denseArgs) -or $denseArgs[2].BoxIndex -ne $cellCapacity-1) {
            throw 'Dense cell CPU ray did not select the last admitted candidate.'
        }
        if ([Math]::Abs($denseArgs[2].Distance - 2.19) -gt .002) { throw 'Dense cell CPU ray hit farther geometry.' }
        $denseIndex = [int](-$denseEncoding.SpatialMin)
        $depth = [int][Math]::Floor($denseEncoding.SpatialDepth / 2)
        $descriptor = ($depth * $denseEncoding.SpatialSize * $denseEncoding.SpatialSize + $denseIndex * $denseEncoding.SpatialSize + $denseIndex) * 2
        $startBytes = $denseEncoding.GridPixels[$descriptor]
        $start = $startBytes.R + 256*$startBytes.G + 65536*$startBytes.B
        $countBytes = $denseEncoding.GridPixels[$descriptor+1]
        $reference = $start + $cellCapacity - 1
        $packedLast = $denseEncoding.GridPixels[$denseEncoding.SpatialSize*$denseEncoding.SpatialSize*$denseEncoding.SpatialDepth*2 + $reference]
        $lastId = $packedLast.R + 256*$packedLast.G + 65536*($packedLast.B -shr 7)
        if (($countBytes.R + 256*$countBytes.G + 65536*$countBytes.B) -ne $cellCapacity -or $lastId -ne $cellCapacity) {
            throw 'Dense cell GPU packing lost the last admitted candidate.'
        }
        $side = if ($cellCapacity -gt 1025) { 1 } else { 65 }
        $pixel = [int][Math]::Floor($side/2)
        $capacitySmokes += @{
            name = "packed-cell-last-reference-$cellCapacity"
            width = $side; height = $side; cellCapacity = $cellCapacity
            acceptedBoxes = $denseEncoding.AcceptedBoxes; omittedBoxes = $denseEncoding.OmittedBoxes
            boxRows = $denseEncoding.BoxRows; gridRows = $denseEncoding.GridRows
            gridLayout = @($denseEncoding.SpatialSize,$denseEncoding.SpatialMin,$denseEncoding.GridRows,$denseEncoding.BoxRows)
            origin = @(.5,-2,1.3); forward = @(0,1,0); right = @(1,0,0); up = @(0,0,1)
            sceneZ = @($denseEncoding.MinZ,$denseEncoding.MaxZ)
            boxPixels = PackedPixels $denseEncoding.BoxPixels; gridPixels = PackedPixels $denseEncoding.GridPixels
            samples = @(@{name='last-candidate';pixel=@($pixel,$pixel);cpuBoxIndex=$denseArgs[2].BoxIndex;expectedRGB=@(255,0,0)})
        }
    }
    $extended = [Activator]::CreateInstance($encodingType, [object[]]@($true))
    $farBoxes = [Array]::CreateInstance($boxType, 1)
    $farBoxes.SetValue($constructor.Invoke(@([System.Numerics.Vector3]::new(0, 20, 1.3),
        [System.Numerics.Vector3]::new(1.15, 0.2, 1.05), [float]0, $blue, $null, $boxShape, [ushort]0, $surfaceAxis, [float]1, $false)), 0)
    $encodingType.GetMethod('Build').Invoke($extended, @(,$farBoxes)) | Out-Null
    $farArgs = @($origin, [System.Numerics.Vector3]::UnitY, $null, $null)
    if (!$encodingType.GetMethod('TryPick').Invoke($extended, $farArgs) -or [Math]::Abs($farArgs[2].Distance - 24.8) -gt .01) {
        throw 'Extended native scene lost its far target.'
    }
    $validation = @{
        parser = $parser.FullName
        assemblyVersion = $assembly.GetName().Version.ToString()
        shaderSha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
        uniforms = @($parsed.Uniforms.Keys)
        vertex = $code.Item1
        fragment = $code.Item2
        library = [System.IO.File]::ReadAllText((Join-Path $shaderDirectory 'z-library.glsl'))
        surfaceSmokes = $surfaceSmokes
        cylinderSmokes = $cylinderSmokes
        alphaSmokes = $alphaSmokes
        wedgeSmokes = $wedgeSmokes
        slantedSmokes = $slantedSmokes
        foliageSmokes = $foliageSmokes
        tiltSmokes = $tiltSmokes
        cellCapacitySmokes = $capacitySmokes
        extendedSmoke = @{
            name = 'extended-first-person-grid'; width = 65; height = 65
            boxRows = $extended.BoxRows; gridRows = $extended.GridRows
            gridLayout = @(64, -32, $extended.GridRows, $extended.BoxRows); radius = 24; tanHalfFov = .767326988
            origin = @(0, -5, 1.3); forward = @(0, 1, 0); right = @(1, 0, 0); up = @(0, 0, 1)
            sceneZ = @($extended.MinZ, $extended.MaxZ)
            boxPixels = PackedPixels $extended.BoxPixels; gridPixels = PackedPixels $extended.GridPixels
            samples = @(@{ name = 'far-blue'; pixel = @(32,32); cpuBoxIndex = 0 }, @{name = 'sky'; pixel = @(60,60); cpuBoxIndex = -1})
        }
        smoke = @{
            width = 65; height = 65
            boxRows = $encodingType.GetField('BoxTextureHeight').GetRawConstantValue()
            origin = @(0, -5, 1.3); forward = @(0, 1, 0); right = @(1, 0, 0); up = @(0, 0, 1)
            sceneZ = @($encoding.MinZ, $encoding.MaxZ)
            boxPixels = PackedPixels $encoding.BoxPixels
            gridPixels = PackedPixels $encoding.GridPixels
            samples = $samples
        }
    }
    # Packed lists vary in length per snapshot. Derive each fixture's own rows
    # from its actual byte payload; reusing another fixture's height is incorrect.
    $gridWidth = [int]$encodingType.GetField('GridTextureWidth').GetRawConstantValue()
    foreach ($scene in @($validation.smoke) + $surfaceSmokes + $cylinderSmokes + $alphaSmokes +
        $wedgeSmokes + $slantedSmokes + $foliageSmokes + $tiltSmokes) {
        $scene.gridRows = [Convert]::FromBase64String($scene.gridPixels).Length / ($gridWidth * 4)
        $scene.gridLayout = @($encoding.SpatialSize,$encoding.SpatialMin,$scene.gridRows,$encoding.BoxRows)
    }
    foreach ($scene in @($validation.smoke,$validation.extendedSmoke) + $surfaceSmokes + $cylinderSmokes + $alphaSmokes +
        $wedgeSmokes + $slantedSmokes + $foliageSmokes + $tiltSmokes + $capacitySmokes) {
        $scene.gridWidth = $gridWidth
        $layoutEncoding = if ($scene.gridLayout[0] -eq 64) { $extended } else { $encoding }
        $scene.gridZ = @($encodingType.GetField('SpatialZMin').GetRawConstantValue(),$layoutEncoding.SpatialZStep,$layoutEncoding.SpatialDepth)
    }
    if ($Benchmark) {
        $benchmarkScenes = @()
        foreach ($radius in @(12,24)) {
            $side = $radius + 1
            $benchBoxes = [Array]::CreateInstance($boxType, $side*$side*7*6)
            $index = 0
            for ($level = -3; $level -le 3; $level++) {
            for ($x = -$radius; $x -le $radius; $x += 2) {
            for ($y = -$radius; $y -le $radius; $y += 2) {
                foreach ($part in @(@(0,0,-.1,.98,.98,.08), @(-.4,-.4,.4,.06,.06,.4),
                    @(.4,-.4,.4,.06,.06,.4), @(-.4,.4,.4,.06,.06,.4), @(.4,.4,.4,.06,.06,.4),
                    @(0,0,.85,.55,.55,.08))) {
                    $benchBoxes.SetValue($constructor.Invoke(@([System.Numerics.Vector3]::new($x+$part[0],$y+$part[1],$level*3+$part[2]),
                        [System.Numerics.Vector3]::new($part[3],$part[4],$part[5]),[float]0,$blue,$null,$boxShape,[ushort]0,$surfaceAxis,[float]1,$false)), $index++)
                }
            } } }
            $benchEncoding = [Activator]::CreateInstance($encodingType, [object[]]@($true))
            $encodingType.GetMethod('Build').Invoke($benchEncoding, @(,$benchBoxes)) | Out-Null
            if ($benchEncoding.AcceptedBoxes -ne $benchBoxes.Length) { throw 'Benchmark scene was truncated.' }
            $pickArgs = @([System.Numerics.Vector3]::new(.8,-$radius+1,1.6), [System.Numerics.Vector3]::UnitY, $null, $null)
            $pickMethod = $encodingType.GetMethod('TryPick')
            for ($sample=0; $sample -lt 20; $sample++) { $pickMethod.Invoke($benchEncoding, $pickArgs) | Out-Null }
            $timer = [Diagnostics.Stopwatch]::StartNew()
            for ($sample=0; $sample -lt 500; $sample++) { $pickMethod.Invoke($benchEncoding, $pickArgs) | Out-Null }
            $timer.Stop()
            $benchmarkScenes += @{
                name="stacked-furniture-radius-$radius"; benchmark=$true; width=1280; height=720
                acceptedBoxes=$benchEncoding.AcceptedBoxes; omittedBoxes=$benchEncoding.OmittedBoxes
                cpuPickMilliseconds=$timer.Elapsed.TotalMilliseconds; cpuPickSamples=500
                boxRows=$benchEncoding.BoxRows; gridRows=$benchEncoding.GridRows; gridWidth=$gridWidth
                gridLayout=@($benchEncoding.SpatialSize,$benchEncoding.SpatialMin,$benchEncoding.GridRows,$benchEncoding.BoxRows)
                gridZ=@($encodingType.GetField('SpatialZMin').GetRawConstantValue(),$benchEncoding.SpatialZStep,$benchEncoding.SpatialDepth)
                radius=$radius; tanHalfFov=.767326988
                origin=@(.8,(-$radius+1),1.6); forward=@(0,1,0); right=@(1,0,0); up=@(0,0,1)
                sceneZ=@($benchEncoding.MinZ,$benchEncoding.MaxZ)
                boxPixels=PackedPixels $benchEncoding.BoxPixels; gridPixels=PackedPixels $benchEncoding.GridPixels
                samples=@()
            }
        }
        $validation.benchmarkScenes = $benchmarkScenes
    }
    $validation | ConvertTo-Json -Depth 6 -Compress
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
