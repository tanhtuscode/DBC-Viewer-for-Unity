using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Adjust these namespaces to match your DBC parser library:
using DbcParserLib;
using DbcParserLib.Model;

public class DbcViewerEditorWindow : EditorWindow
{
    private string dbcFilePath = "";
    private Dbc loadedDbc = null;

    // Scroll positions for messages and signals sections.
    private Vector2 scrollPosMessages;
    private Vector2 scrollPosSignals;

    // Currently selected message index.
    private int selectedMessageIndex = -1;

    // For searching messages by ID
    private string searchID = "";

    // Height of the messages area, adjustable via splitter
    private float messagesHeight = 150f;
    private bool isResizingSplitter = false; // track if we are dragging

    [MenuItem("Tools/DBC Viewer Window")]
    public static void ShowWindow()
    {
        var window = GetWindow<DbcViewerEditorWindow>();
        window.titleContent = new GUIContent("DBC Viewer");
        window.Show();
    }

    private void OnGUI()
    {
        // DBC Path and Load Button at top:
        EditorGUILayout.LabelField("DBC Path:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        dbcFilePath = EditorGUILayout.TextField(dbcFilePath);
        if (GUILayout.Button("Load DBC", GUILayout.Width(100)))
        {
            LoadDbcFile(dbcFilePath);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (loadedDbc == null)
        {
            EditorGUILayout.HelpBox("No DBC loaded. Enter path and click Load DBC.", MessageType.Info);
            return;
        }

        // Show a small summary: node names on one line
        if (loadedDbc.Nodes != null && loadedDbc.Nodes.Any())
        {
            EditorGUILayout.LabelField("Nodes:", EditorStyles.boldLabel);
            string nodeNames = string.Join(", ", loadedDbc.Nodes.Select(n => n.Name));
            EditorGUILayout.LabelField(nodeNames);
        }
        else
        {
            EditorGUILayout.LabelField("No Nodes found in the DBC.", EditorStyles.boldLabel);
        }

        GUILayout.Space(10);

        // Add a search bar for message ID
        EditorGUILayout.LabelField("Find Message by ID (e.g. 0x340 or 340):", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        searchID = EditorGUILayout.TextField(searchID);
        if (GUILayout.Button("Find", GUILayout.Width(60)))
        {
            FindMessageById(searchID);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        // MESSAGES section
        EditorGUILayout.LabelField("Messages:", EditorStyles.boldLabel);

        // Use messagesHeight for the scrollview
        scrollPosMessages = EditorGUILayout.BeginScrollView(scrollPosMessages, GUILayout.Height(messagesHeight));

        if (loadedDbc.Messages != null)
        {
            // Draw header row
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Index", GUILayout.Width(40));
            EditorGUILayout.LabelField("ID", GUILayout.Width(60));
            EditorGUILayout.LabelField("Name", GUILayout.Width(150));
            EditorGUILayout.LabelField("DLC", GUILayout.Width(40));
            EditorGUILayout.LabelField("Transmitter", GUILayout.Width(100));
            EditorGUILayout.LabelField("Comment", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            var messagesList = loadedDbc.Messages.ToList();
            for (int i = 0; i < messagesList.Count; i++)
            {
                var msg = messagesList[i];
                bool isSelected = (i == selectedMessageIndex);

                // Row background style (highlight selected row)
                GUIStyle rowStyle = isSelected
                    ? new GUIStyle(EditorStyles.textField)
                    {
                        normal = { background = MakeTex(2, 2, new Color(0.2f, 0.5f, 1f, 0.3f)) }
                    }
                    : EditorStyles.textField;

                EditorGUILayout.BeginHorizontal(rowStyle);

                // Each cell is drawn with a "button" so it's clickable.
                DrawCell(i.ToString(), 40, () => { selectedMessageIndex = i; });
                DrawCell("0x" + msg.ID.ToString("X"), 60, () => { selectedMessageIndex = i; });
                DrawCell(msg.Name, 150, () => { selectedMessageIndex = i; });
                DrawCell(msg.DLC.ToString(), 40, () => { selectedMessageIndex = i; });
                DrawCell(msg.Transmitter, 100, () => { selectedMessageIndex = i; });
                DrawCell(msg.Comment ?? "", 0, () => { selectedMessageIndex = i; }, expandWidth: true);

                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndScrollView();

        // Now draw a "splitter" for vertical resizing
        Rect splitterRect = GUILayoutUtility.GetRect(position.width, 5f, GUIStyle.none);
        EditorGUI.DrawRect(splitterRect, Color.gray);

        // Handle events for dragging
        Event e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown:
                if (splitterRect.Contains(e.mousePosition))
                {
                    isResizingSplitter = true;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (isResizingSplitter)
                {
                    // Adjust messagesHeight by the vertical delta
                    messagesHeight += e.delta.y;
                    // clamp so it doesn't go negative or too big
                    messagesHeight = Mathf.Clamp(messagesHeight, 50f, position.height - 200f);
                    Repaint();
                }
                break;

            case EventType.MouseUp:
                if (isResizingSplitter)
                {
                    isResizingSplitter = false;
                    e.Use();
                }
                break;
        }

        GUILayout.Space(5);

        // SIGNALS section for the selected message.
        EditorGUILayout.LabelField("Signals:", EditorStyles.boldLabel);
        scrollPosSignals = EditorGUILayout.BeginScrollView(scrollPosSignals, GUILayout.Height(position.height - 300)); 
        // The "300" is a rough offset to keep things from overflowing; adjust as needed.

        if (selectedMessageIndex >= 0 && loadedDbc.Messages.Count() > selectedMessageIndex)
        {
            var selectedMsg = loadedDbc.Messages.ElementAt(selectedMessageIndex);
            var signalsList = selectedMsg.Signals.ToList();

            // Draw header row for signals.
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Name", GUILayout.Width(150));
            EditorGUILayout.LabelField("StartBit", GUILayout.Width(60));
            EditorGUILayout.LabelField("Length", GUILayout.Width(50));
            EditorGUILayout.LabelField("ByteOrder", GUILayout.Width(80));
            EditorGUILayout.LabelField("Signed", GUILayout.Width(50));
            EditorGUILayout.LabelField("Factor", GUILayout.Width(50));
            EditorGUILayout.LabelField("Offset", GUILayout.Width(50));
            EditorGUILayout.LabelField("Min", GUILayout.Width(50));
            EditorGUILayout.LabelField("Max", GUILayout.Width(50));
            EditorGUILayout.LabelField("Unit", GUILayout.Width(60));
            EditorGUILayout.LabelField("Comment", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            // For each signal, draw a row.
            foreach (var sig in signalsList)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.textField);
                EditorGUILayout.LabelField(sig.Name, GUILayout.Width(150));
                EditorGUILayout.LabelField(sig.StartBit.ToString(), GUILayout.Width(60));
                EditorGUILayout.LabelField(sig.Length.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField(sig.ByteOrder.ToString(), GUILayout.Width(80));
                // Use the property that indicates whether the signal is signed or not
                EditorGUILayout.LabelField(sig.ValueType.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField(sig.Factor.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField(sig.Offset.ToString(), GUILayout.Width(50));
                // If Minimum/Maximum are plain doubles, handle NaN if needed
                string minText = double.IsNaN(sig.Minimum) ? "" : sig.Minimum.ToString();
                string maxText = double.IsNaN(sig.Maximum) ? "" : sig.Maximum.ToString();
                EditorGUILayout.LabelField(minText, GUILayout.Width(50));
                EditorGUILayout.LabelField(maxText, GUILayout.Width(50));
                EditorGUILayout.LabelField(sig.Unit ?? "", GUILayout.Width(60));
                EditorGUILayout.LabelField(sig.Comment ?? "", GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void LoadDbcFile(string path)
    {
        try
        {
            loadedDbc = Parser.ParseFromPath(path);
            selectedMessageIndex = -1;
            Debug.Log("DBC loaded successfully: " + path);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to load DBC: " + ex.Message);
            loadedDbc = null;
        }
    }

    /// <summary>
    /// Finds the first message whose ID in hex partially matches searchID 
    /// or exactly matches (depending on the approach).
    /// </summary>
    private void FindMessageById(string searchID)
    {
        if (loadedDbc == null || loadedDbc.Messages == null)
            return;

        var messagesList = loadedDbc.Messages.ToList();
        // Simple approach: parse user input if it starts with "0x" or not
        string searchUpper = searchID.Replace("0x", "").Trim().ToUpperInvariant();

        // Find index where message's hex ID contains the search text
        int foundIndex = messagesList.FindIndex(m => 
        {
            string hexId = m.ID.ToString("X");
            return hexId.Contains(searchUpper);
        });

        if (foundIndex >= 0)
        {
            selectedMessageIndex = foundIndex;
            Debug.Log($"Found message at index {foundIndex}, ID=0x{messagesList[foundIndex].ID:X}");
        }
        else
        {
            Debug.LogWarning("No message found matching ID: " + searchID);
        }
    }

    // Utility method to draw a cell with a "label button" style so it's clickable
    private void DrawCell(string text, float width, System.Action onClick, bool expandWidth = false)
    {
        if (expandWidth)
        {
            if (GUILayout.Button(text, EditorStyles.label, GUILayout.ExpandWidth(true)))
            {
                onClick?.Invoke();
            }
        }
        else
        {
            if (width > 0)
            {
                if (GUILayout.Button(text, EditorStyles.label, GUILayout.Width(width)))
                {
                    onClick?.Invoke();
                }
            }
            else
            {
                // Fallback if width is zero but expandWidth is false
                if (GUILayout.Button(text, EditorStyles.label))
                {
                    onClick?.Invoke();
                }
            }
        }
    }

    // Utility method to create a solid-color texture for highlighting selected row
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
