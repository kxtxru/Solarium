using System.Collections;
using System.IO;
using Unity.MLAgents;
using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class AutomatedShowcaseCapture3D : MonoBehaviour
    {
        private IEnumerator Start()
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            int flag = System.Array.IndexOf(arguments, "-solarium-capture");
            bool smokeExit = System.Array.IndexOf(arguments, "-solarium-smoke-exit") >= 0;
            if (flag < 0 && !smokeExit)
                yield break;
            Application.runInBackground = true;
            string path = flag >= 0 && flag + 1 < arguments.Length
                ? arguments[flag + 1]
                : Path.Combine(Application.persistentDataPath, "solarium3d-capture.png");

            // Release builds run uncapped while hidden, so frame counts can finish
            // before one physics step. Wait real time to capture an active episode.
            yield return new WaitForSecondsRealtime(2f);
            yield return new WaitForEndOfFrame();

            if (flag >= 0)
                CaptureCamera(path);

            // Freeze fixed steps without disabling an agent while its asynchronous
            // inference decision may still be completing.
            Time.timeScale = 0f;
            for (int i = 0; i < 120; i++)
                yield return new WaitForEndOfFrame();

            // The automated capture exits much sooner than a normal player session.
            // Shut down policies first, then the shared model runner, so inference
            // GPU buffers are released deterministically before Application.Quit.
            foreach (SolAgent3D agent in FindObjectsByType<SolAgent3D>())
                agent.enabled = false;
            if (Academy.IsInitialized)
                Academy.Instance.Dispose();
            yield return null;
            Application.Quit(0);
        }

        private void CaptureCamera(string path)
        {
            Camera targetCamera = GetComponent<Camera>() ?? Camera.main;
            if (targetCamera == null)
                return;
            Canvas.ForceUpdateCanvases();

            const int width = 1280;
            const int height = 720;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = targetCamera.targetTexture;
            try
            {
                targetCamera.targetTexture = target;
                targetCamera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply(false, false);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log($"Solarium 3D showcase captured at {path}");
            }
            finally
            {
                targetCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Destroy(target);
                Destroy(texture);
            }
        }
    }
}
