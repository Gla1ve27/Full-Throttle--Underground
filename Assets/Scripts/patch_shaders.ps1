$content = Get-Content 'Assets/EasyRoads3D/shaders/ER Road.shader' -Raw
$content = $content -replace 'ApplyDecalToSurfaceData\(decalSurfaceData, fragInputs.tangentToWorld\[2\], surfaceData\);', '#if UNITY_VERSION >= 202120
					ApplyDecalToSurfaceData(decalSurfaceData, fragInputs.tangentToWorld[2], surfaceData, normalTS);
#else
					ApplyDecalToSurfaceData(decalSurfaceData, fragInputs.tangentToWorld[2], surfaceData);
#endif'
Set-Content 'Assets/EasyRoads3D/shaders/ER Road.shader' $content

$content2 = Get-Content 'Assets/EasyRoads3D/shaders/ER Surface.shader' -Raw
$content2 = $content2 -replace 'ApplyDecalToSurfaceData\(decalSurfaceData, fragInputs.tangentToWorld\[2\], surfaceData\);', '#if UNITY_VERSION >= 202120
					ApplyDecalToSurfaceData(decalSurfaceData, fragInputs.tangentToWorld[2], surfaceData, normalTS);
#else
					ApplyDecalToSurfaceData(decalSurfaceData, fragInputs.tangentToWorld[2], surfaceData);
#endif'
Set-Content 'Assets/EasyRoads3D/shaders/ER Surface.shader' $content2
