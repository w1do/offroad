using UnityEngine;

public sealed class DriveHUD : MonoBehaviour
{
    public MountainSUV car;
    private GUIStyle title;
    private GUIStyle text;
    private Texture2D panel;

    private void OnGUI()
    {
        if (car == null) return;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            text = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            text.normal.textColor = new Color(0.88f, 0.93f, 0.94f);
            panel = new Texture2D(1, 1);
            panel.SetPixel(0, 0, new Color(0.025f, 0.055f, 0.065f, 0.87f)); panel.Apply();
        }
        GUI.DrawTexture(new Rect(22, 22, 330, 158), panel);
        GUI.Label(new Rect(40, 32, 300, 40), "MOUNTAIN PASS", title);
        string surface = car.surface == DriveSurface.Ice ? "ICE / LOW GRIP" : car.surface == DriveSurface.Broken ? "BROKEN ASPHALT" : car.surface == DriveSurface.Asphalt ? "ASPHALT" : "GRAVEL / OFF ROAD";
        GUI.Label(new Rect(40, 76, 300, 30), Mathf.RoundToInt(car.SpeedKmh) + " km/h    /    " + surface, text);
        GUI.Label(new Rect(40, 106, 300, 30), "PASS " + Mathf.RoundToInt(car.Progress * 100) + "%    /    " + (car.route.length / 1000).ToString("F1") + " km", text);
        GUI.color = new Color(0.35f, 0.83f, 0.75f);
        GUI.DrawTexture(new Rect(40, 151, 290 * car.Progress, 4), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(22, Screen.height - 61, 720, 39), panel);
        GUI.Label(new Rect(38, Screen.height - 53, 710, 30), "WASD / ARROWS  Drive     SPACE  Handbrake     R  Recover     C  Camera", text);
        if (car.Progress > 0.992f) GUI.Label(new Rect(Screen.width / 2 - 150, 50, 400, 60), "PASS COMPLETE", title);
    }
}
