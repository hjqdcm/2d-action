using System;
using UnityEditor;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class BeastParticlesValidation
    {
        public static void CaptureActual(SlashArena arena,Vector3 focus,float zoom,string file)
        {
            var fx=arena.Travel.ActiveMap.GetComponent<BeastAttackParticles>(); fx.Refresh();
            var camera=arena.WorldCamera; var old=camera.transform.position; float oldZoom=camera.orthographicSize;
            camera.transform.position=focus-ObliqueProjection.Forward(arena.View.elevation)*20; camera.orthographicSize=zoom;
            var shown=ManorOcclusionValidation.Capture(camera); fx.Renderer.enabled=false;
            var hidden=ManorOcclusionValidation.Capture(camera); fx.Renderer.enabled=true;
            camera.transform.position=old; camera.orthographicSize=oldZoom;
            var a=shown.GetPixels32(); var b=hidden.GetPixels32(); int changed=0;
            for(int i=0;i<a.Length;i++) if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>12) changed++;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
            System.IO.File.WriteAllBytes(file,shown.EncodeToPNG());
            UnityEngine.Object.Destroy(shown); UnityEngine.Object.Destroy(hidden);
            if(changed<20) throw new Exception("Actual attack particles are not visible: "+file+", pixels="+changed);
            Debug.Log("BEAST_ATTACK_RENDER_PASS: "+file+" changes "+changed+" pixels.");
        }
        public static void Verify(SlashArena arena,Vector2 point)
        {
            var fx=arena.Travel.ActiveMap.GetComponent<BeastAttackParticles>();
            if(fx==null || ShaderUtil.ShaderHasError(fx.Renderer.sharedMaterial.shader)) throw new Exception("Missing beast particle renderer/shader.");
            fx.Clear(); fx.DustBurst(point,24); fx.Advance(.08f);
            if(fx.ActiveCount!=24 || !fx.Renderer.enabled) throw new Exception("Dust burst did not produce visible particle geometry.");
            var camera=arena.WorldCamera; var old=camera.transform.position; float zoom=camera.orthographicSize;
            camera.transform.position=(Vector3)point+arena.View.Up*.45f-ObliqueProjection.Forward(arena.View.elevation)*20;
            camera.orthographicSize=2.5f;
            var shown=ManorOcclusionValidation.Capture(camera); fx.Renderer.enabled=false;
            var hidden=ManorOcclusionValidation.Capture(camera); fx.Renderer.enabled=true;
            camera.transform.position=old; camera.orthographicSize=zoom;
            var a=shown.GetPixels32(); var b=hidden.GetPixels32(); int changed=0;
            for(int i=0;i<a.Length;i++) if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>12) changed++;
            UnityEngine.Object.Destroy(shown); UnityEngine.Object.Destroy(hidden);
            if(changed<30) throw new Exception("New dust particles are not visible in the real render: "+changed);
            float clock=fx.Clock; int count=fx.ActiveCount;
            arena.TogglePause(); fx.Advance(.5f);
            if(fx.Clock!=clock || fx.ActiveCount!=count) throw new Exception("Pause advanced particles."); arena.TogglePause();
            fx.DustBurst(point,BeastAttackParticles.MaxParticles*2);
            if(fx.ActiveCount!=BeastAttackParticles.MaxParticles) throw new Exception("Particle budget was not enforced.");
            fx.Advance(2);
            if(fx.ActiveCount!=0 || fx.Renderer.enabled) throw new Exception("Expired particles leaked.");
            Debug.Log("BEAST_PARTICLES_PASS: actual dust rendering ("+changed+" pixels), pause freeze, bounded particle count and lifetime cleanup.");
        }
    }
}
