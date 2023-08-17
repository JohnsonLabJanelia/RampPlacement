using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

#if UNITY_EDITOR

public class RampPlacement : EditorWindow
{
    [MenuItem("Window/Ramp Placement")]
    public static void ShowWindow()
    {
        RampPlacement win = (RampPlacement)GetWindow(typeof(RampPlacement));
        win.Init();
    }

    public void Init()
    {
        Debug.Log("RampPlacement.Init");

        _surface = GameObject.Find("surface");

        _armA = GameObject.Find(EditorPrefs.GetString(EDITOR_PREF_KEY_ARM_NAME_A, "grimlock"));
        _armB = GameObject.Find(EditorPrefs.GetString(EDITOR_PREF_KEY_ARM_NAME_B, "optimus"));
        _armC = GameObject.Find(EditorPrefs.GetString(EDITOR_PREF_KEY_ARM_NAME_C, "bumblebee"));
        ArmChanged();

        _ramp = GameObject.Find(EditorPrefs.GetString(EDITOR_PREF_KEY_RAMP_NAME, "marker_ramp"));
        RampChanged();

        // A hack to work around how it is difficult to force changes to editor prefs.
        bool force = false;

        bool hx = EditorPrefs.HasKey(EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_X);
        bool hy = EditorPrefs.HasKey(EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_Y);
        bool hz = EditorPrefs.HasKey(EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_Z);
        if (force || !hx || !hy || !hz)
        {
            float z = (float)System.Math.Round(-0.6f * _rampExtents.z, 4);
            _rampHandleOffset = new Vector3(0, 0, z);
            EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_X, _rampHandleOffset.x);
            EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_Y, _rampHandleOffset.y);
            EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_Z, _rampHandleOffset.z);
        }

        bool bx = EditorPrefs.HasKey(EDITOR_PREF_KEY_RAMP_BALL_OFFSET_X);
        bool by = EditorPrefs.HasKey(EDITOR_PREF_KEY_RAMP_BALL_OFFSET_Y);
        bool bz = EditorPrefs.HasKey(EDITOR_PREF_KEY_RAMP_BALL_OFFSET_Z);
        if (force || !bx || !by || !bz)
        {
            float y = (float)System.Math.Round(_rampExtents.y, 4);
            float z = (float)System.Math.Round(-0.6f * _rampExtents.z, 4);
            _rampBallOffset = new Vector3(0, y, z);
            EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_BALL_OFFSET_X, _rampBallOffset.x);
            EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_BALL_OFFSET_Y, _rampBallOffset.y);
            EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_BALL_OFFSET_Z, _rampBallOffset.z);
        }

        if (RampPlacement._placements.Count == 0)
        {
            AddPlacement();
        }

        Debug.Log("Starting DuringSceneGui");
        SceneView.duringSceneGui += DuringSceneGui;
    }

    private void OnDestroy()
    {
        SceneView.duringSceneGui -= DuringSceneGui;
    }

    public void OnGUI()
    {

        EditorGUILayout.BeginVertical();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Open"))
        {
            Open();
        }

        if (GUILayout.Button("Save"))
        {
            Save();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        OnGuiObject(ref _ramp, EDITOR_PREF_KEY_RAMP_NAME, "Ramp");
        if (EditorGUI.EndChangeCheck())
        {
            RampChanged();
        }

        OnGuiVector3(ref _rampHandleOffset, EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_X, EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_Y, EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_Z, "Ramp handle offset");
        OnGuiVector3(ref _rampBallOffset, EDITOR_PREF_KEY_RAMP_BALL_OFFSET_X, EDITOR_PREF_KEY_RAMP_BALL_OFFSET_Y,EDITOR_PREF_KEY_RAMP_BALL_OFFSET_Z, "Ramp ball offset");

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        OnGuiObject(ref _armA, EDITOR_PREF_KEY_ARM_NAME_A, "Arm A");
        OnGuiObject(ref _armB, EDITOR_PREF_KEY_ARM_NAME_B, "Arm B");
        OnGuiObject(ref _armC, EDITOR_PREF_KEY_ARM_NAME_C, "Arm C");
        if (EditorGUI.EndChangeCheck())
        {
            ArmChanged();
        }

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();
        if (EditorPrefs.HasKey(EDITOR_PREF_KEY_CHOSEN_ARM))
        {
            _iChosenArm = EditorPrefs.GetInt(EDITOR_PREF_KEY_CHOSEN_ARM);
        }
        for (int i = 0; i < 3; ++i)
        {
            if (GUILayout.Toggle((i == _iChosenArm), _chosenArmLabels[i]))
            {
                if (i != _iChosenArm)
                {
                    _iChosenArm = i;
                    EditorPrefs.SetInt(EDITOR_PREF_KEY_CHOSEN_ARM, _iChosenArm);

                    MoveRampToSafePosition();

                    // Force updating of the line to the arm and the arc showing maximum reach.
                    EditorWindow view = EditorWindow.GetWindow<SceneView>();
                    view.Repaint();
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Previous"))
        {
            if (_current > 0)
            {
                _current -= 1;
                RestorePlacement();
            }
        }

        string countLabel = (_current + 1) + " of " + _placements.Count;
        EditorGUILayout.LabelField(countLabel);

        if (GUILayout.Button("Next"))
        {
            if (_current < _placements.Count - 1)
            {
                _current += 1;
                RestorePlacement();
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Before"))
        {
            AddPlacement(false);
            RandomizePosition(_ramp);
        }
        if (GUILayout.Button("Add After"))
        {
            AddPlacement(true);
            RandomizePosition(_ramp);
        }

        if (EditorPrefs.HasKey(EDITOR_PREF_KEY_ADD_RANDOM))
        {
            _addRandom = EditorPrefs.GetBool(EDITOR_PREF_KEY_ADD_RANDOM);
        }
        _addRandom = GUILayout.Toggle(_addRandom, "Randomize placement");
        EditorPrefs.SetBool(EDITOR_PREF_KEY_ADD_RANDOM, _addRandom);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(_placements.Count <= 1);
        if (GUILayout.Button("Delete"))
        {
            if (EditorUtility.DisplayDialog("Delete?", "Delete the current placement?", "Delete", "Cancel"))
            {
                DeletePlacement();
            }                
        }

        if (GUILayout.Button("Clear"))
        {
            if (EditorUtility.DisplayDialog("Clear?", "Clear all placements?", "Clear", "Cancel"))
            {
                ClearPlacements();
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();
    }

    private void OnGuiObject(ref GameObject obj, string prefKey, string label)
    {
        if (obj == null)
        {
            obj = GameObject.Find(EditorPrefs.GetString(prefKey));
        }
        obj = (GameObject)EditorGUILayout.ObjectField(label, obj, typeof(GameObject), true);
        if (obj != null)
        {
            EditorPrefs.SetString(prefKey, obj.name);
        }
    }

    private void OnGuiVector3(ref Vector3 v, string prefKeyX, string prefKeyY, string prefKeyZ, string label)
    {
        if (EditorPrefs.HasKey(prefKeyX))
        {
            float x = EditorPrefs.GetFloat(prefKeyX);
            v.x = x;
        }
        if (EditorPrefs.HasKey(prefKeyY))
        {
            float y = EditorPrefs.GetFloat(prefKeyY);
            v.y = y;
        }
        if (EditorPrefs.HasKey(prefKeyZ))
        {
            float z = EditorPrefs.GetFloat(prefKeyZ);
            v.z = z;
        }
        v = EditorGUILayout.Vector3Field(label, v);
        EditorPrefs.SetFloat(prefKeyX, v.x);
        EditorPrefs.SetFloat(prefKeyY, v.y);
        EditorPrefs.SetFloat(prefKeyZ, v.z);
    }

    private void DuringSceneGui(SceneView view)
    {
        Vector3 p = _ramp.transform.position;
        p.y = _surface.transform.position.y + _rampExtents.y;
        _ramp.transform.position = p;

        Vector3 e = _ramp.transform.localEulerAngles;
        e.x = e.z = 0;
        _ramp.transform.localEulerAngles = e;

        _ramp.transform.localScale = Vector3.one;

        GameObject armBase = (_iChosenArm == 0) ? _armBaseA : (_iChosenArm == 1) ? _armBaseB : _armBaseC;
        Vector3 basePos = armBase.transform.position;

        Handles.DrawWireArc(basePos, Vector3.up, -armBase.transform.forward, 180, _rampRadiusMax);
        Handles.DrawLine(basePos, _ramp.transform.position);

        if (_ramp.transform.position != _rampPosSafe)
        {
            if (IsSafe(_ramp.transform.position, basePos))
            {
                _rampPosSafe = _ramp.transform.position;
                PointAtCenter(_ramp);
                StorePlacement();
            }
            else
            {
                _ramp.transform.position = _rampPosSafe;
            }
        }

        if (_ramp.transform.eulerAngles.y != GetPlacementRampAngleY())
        {
            StorePlacement();
        }
    }

    private bool IsSafe(Vector3 rampPos, Vector3 basePos)
    {
        float dx = Mathf.Abs(rampPos.x - _surface.transform.position.x);
        float dz = Mathf.Abs(rampPos.z - _surface.transform.position.z);
        float d = _surface.transform.localScale.x / 2 - _rampExtents.z;
        if ((dx > d) || (dz > d))
        {
            return false;
        }
        if (Vector3.Distance(rampPos, basePos) > _rampRadiusMax)
        {
            return false;
        }
        return true;
    }

    private void PointAtCenter(GameObject obj)
    {
        Vector3 centerPos = _surface.transform.position;
        Vector3 objToCenter = centerPos - obj.transform.position;
        objToCenter.y = 0;
        float angle = Vector3.SignedAngle(Vector3.forward, objToCenter, Vector3.up);

        obj.transform.localEulerAngles = new Vector3(0, angle, 0);
    }

    private void RandomizePosition(GameObject obj)
    {
        if (!_addRandom)
        {
            return;
        }

        GameObject armBase = (_iChosenArm == 0) ? _armBaseA : (_iChosenArm == 1) ? _armBaseB : _armBaseC;
        Vector3 basePos = armBase.transform.position;
        Vector3 pos = Vector3.zero;

        do
        {
            Vector3 v = armBase.transform.right;
            float angle = UnityEngine.Random.Range(0, 360);
            Vector3 vRotated = Matrix4x4.Rotate(Quaternion.Euler(0, angle, 0)).MultiplyVector(v);
            float s = _surface.transform.localScale.x;
            float r = UnityEngine.Random.Range(0.1f * s, 0.5f * s);
            pos = obj.transform.position + r * vRotated;
        }
        while (!IsSafe(pos, basePos));

        obj.transform.position = pos;
        PointAtCenter(obj);
    }

    private void MoveRampToSafePosition()
    {
        GameObject armBase = (_iChosenArm == 0) ? _armBaseA : (_iChosenArm == 1) ? _armBaseB : _armBaseC;
        Vector3 basePos = armBase.transform.position;
        Vector3 pos = _ramp.transform.position;
        if (!IsSafe(pos, basePos))
        {
            Vector3 v = (pos - basePos).normalized;
            pos = basePos + _rampRadiusMax * v;
            _ramp.transform.position = pos;
            _rampPosSafe = pos;
        }
    }

    private void Open()
    {
        _jsonPath = EditorUtility.OpenFilePanel("Open placement file", "", "json");
        if (_jsonPath.Length != 0)
        {
            LoadJson();
            EditorPrefs.SetString(EDITOR_PREF_KEY_JSON_PATH, _jsonPath);
        }
    }

    private void Save()
    {
        if (EditorPrefs.HasKey(EDITOR_PREF_KEY_JSON_PATH))
        {
            _jsonPath = EditorPrefs.GetString(EDITOR_PREF_KEY_JSON_PATH);
        }
        string dir = "";
        if (_jsonPath.Length != 0)
        {
            dir = Path.GetDirectoryName(_jsonPath);
        }
        _jsonPath = EditorUtility.SaveFilePanel("Save placement file", dir, _jsonPath, "json");
        if (_jsonPath.Length != 0)
        {
            SaveJson();
            EditorPrefs.SetString(EDITOR_PREF_KEY_JSON_PATH, _jsonPath);
        }
    }

    private void LoadJson()
    {
        // Filter out comment lines starting with "//" or "#".
        string[] jsonLines = File.ReadAllLines(_jsonPath)
            .Where(l => !l.Trim().StartsWith("//") && !l.Trim().StartsWith("#"))
            .ToArray();
        string json = String.Join(" ", jsonLines);

        PlacementsSaved allSaved = new PlacementsSaved();
        JsonUtility.FromJsonOverwrite(json, allSaved);

        _ramp = GameObject.Find(allSaved.rampName);

        _rampHandleOffset = allSaved.rampHandleOffset;
        EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_X, _rampHandleOffset.x);
        EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_Y, _rampHandleOffset.y);
        EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_Z, _rampHandleOffset.z);

        _rampBallOffset = allSaved.rampBallOffset;
        EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_BALL_OFFSET_X, _rampBallOffset.x);
        EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_BALL_OFFSET_Y, _rampBallOffset.y);
        EditorPrefs.SetFloat(EDITOR_PREF_KEY_RAMP_BALL_OFFSET_Z, _rampBallOffset.z);

        _placements.Clear();
        foreach (PlacementSaved saved in allSaved.placements)
        {
            GameObject arm = GameObject.Find(saved.reachingArmName);
            int iChosenArm = (arm == _armA) ? 0 : (arm == _armB) ? 1 : 2;
            Vector3 rampPosition = saved.rampPosition;
            float rampAngleY = saved.rampAngleY;
            Placement placement = new Placement(iChosenArm, rampPosition, rampAngleY);
            _placements.Add(placement);
        }

        _current = 0;
        RestorePlacement();
    }

    private void SaveJson()
    {
        Debug.Log("Saving to '" + _jsonPath + "'");

        PlacementsSaved allToBeSaved = new PlacementsSaved();
        allToBeSaved.rampName = _ramp.name;
        allToBeSaved.rampHandleOffset = _rampHandleOffset;
        allToBeSaved.rampBallOffset = _rampBallOffset;
        foreach (Placement placement in _placements)
        {
            GameObject arm = (placement.iChosenArm == 0) ? _armA : (placement.iChosenArm == 1) ? _armB : _armC;
            string armName = arm.name;
            Vector3 armBase = arm.transform.position;
            Vector3 rampPosition = placement.rampPosition;
            float rampAngleY = placement.rampAngleY;

            Matrix4x4 rot = Matrix4x4.Rotate(Quaternion.Euler(0, rampAngleY, 0));
            Vector3 handlePos = rampPosition + rot.MultiplyVector(_rampHandleOffset);
            Vector3 ballPos = rampPosition + rot.MultiplyVector(_rampBallOffset);

            PlacementSaved toBeSaved = new PlacementSaved(armName, armBase, rampPosition, rampAngleY, handlePos, ballPos);
            allToBeSaved.placements.Add(toBeSaved);
        }
        string json = JsonUtility.ToJson(allToBeSaved, true);
        File.WriteAllText(_jsonPath, json);
    }

    private void AddPlacement(bool after = true)
    {
        Placement placement = new Placement(_iChosenArm, _ramp.transform.position, _ramp.transform.eulerAngles.y);
        if (after)
        {
            if (_current == _placements.Count - 1)
            {
                _placements.Add(placement);
            }
            else
            {
                _placements.Insert(_current + 1, placement);
            }
            _current += 1;
        }
        else
        {
            _placements.Insert(_current, placement);
        }
    }

    private void DeletePlacement()
    {
        _placements.RemoveAt(_current);
        if (_current == _placements.Count)
        {
            _current -= 1;
        }
        RestorePlacement();
    }

    private void ClearPlacements()
    {
        _placements.Clear();
        _current = -1;
        AddPlacement();
    }
    
    private void StorePlacement()
    {
        _placements[_current].iChosenArm = _iChosenArm;
        _placements[_current].rampPosition = _ramp.transform.position;
        _placements[_current].rampAngleY = _ramp.transform.eulerAngles.y;
    }

    private void RestorePlacement()
    {
        _iChosenArm = _placements[_current].iChosenArm;
        _ramp.transform.position = _placements[_current].rampPosition;
        _ramp.transform.eulerAngles = new Vector3(0, _placements[_current].rampAngleY, 0);

        _rampPosSafe = _ramp.transform.position;

        EditorPrefs.SetInt(EDITOR_PREF_KEY_CHOSEN_ARM, _iChosenArm);
    }

    private float GetPlacementRampAngleY()
    {
        return _placements[_current].rampAngleY;
    }

    private GameObject GetArmBase(GameObject arm)
    {
        if (arm != null)
        {
            Transform found = FindRecursive("base", arm.transform);
            if (found != null)
            {
                return found.gameObject;
            }
        }
        return null;
    }

    private Transform FindRecursive(string name, Transform current)
    {
        if (current.name == name)
        {
            return current;
        }
        foreach (Transform child in current)
        {
            Transform found = FindRecursive(name, child);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private void ArmChanged()
    {
        _armBaseA = GetArmBase(_armA);
        _armBaseB = GetArmBase(_armB);
        _armBaseC = GetArmBase(_armC);
    }

    private void RampChanged()
    {
        Collider collider = _ramp.GetComponent<Collider>();
        if (collider == null)
        {
            for (int i = 0; i < _ramp.transform.childCount; i += 1)
            {
                Transform child = _ramp.transform.GetChild(i);
                if (child.name.Contains("ramp"))
                {
                    collider = child.gameObject.GetComponent<Collider>();
                    if (collider != null)
                    {
                        break;
                    }
                }
            }
        }
        if (collider != null)
        {
            _rampExtents = collider.bounds.extents;

            Debug.Log("_rampExtents " + _rampExtents.ToString("F4"));
        }
    }

    private string _jsonPath = "";
    private const string EDITOR_PREF_KEY_JSON_PATH = "RampPlacementJsonPath";

    private const string EDITOR_PREF_KEY_ARM_NAME_A = "RampPlacementArmNameA";
    private GameObject _armA;
    private GameObject _armBaseA;

    private const string EDITOR_PREF_KEY_ARM_NAME_B = "RampPlacementArmNameB";
    private GameObject _armB;
    private GameObject _armBaseB;

    private const string EDITOR_PREF_KEY_ARM_NAME_C = "RampPlacementArmNameC";
    private GameObject _armC;
    private GameObject _armBaseC;

    private const string EDITOR_PREF_KEY_RAMP_NAME = "RampPlacementRampName";
    private GameObject _ramp;
    private Vector3 _rampExtents;
    private Vector3 _rampPosSafe;

    private GameObject _surface;

    private Vector3 _rampHandleOffset;
    private const string EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_X = "RampPlacementRampHandleOffsetX";
    private const string EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_Y = "RampPlacementRampHandleOffsetY";
    private const string EDITOR_PREF_KEY_RAMP_HANDLE_OFFSET_Z = "RampPlacementRampHandleOffsetZ";

    private Vector3 _rampBallOffset;
    private const string EDITOR_PREF_KEY_RAMP_BALL_OFFSET_X = "RampPlacementRampBallOffsetX";
    private const string EDITOR_PREF_KEY_RAMP_BALL_OFFSET_Y = "RampPlacementRampBallOffsetY";
    private const string EDITOR_PREF_KEY_RAMP_BALL_OFFSET_Z = "RampPlacementRampBallOffsetZ";

    private int _iChosenArm = 0;
    private string[] _chosenArmLabels = new string[] { "Arm A", "Arm B", "Arm C" };
    private const string EDITOR_PREF_KEY_CHOSEN_ARM = "RampPlacementChosenArm";
    
    private float _rampRadiusMin = 0.4f;
    private float _rampRadiusMax = 1.3f;

    private bool _addRandom;
    private const string EDITOR_PREF_KEY_ADD_RANDOM = "RampPlacementAddRandom";

    internal class Placement
    {
        internal Placement(int i, Vector3 p, float ay)
        {
            iChosenArm = i;
            rampPosition = p;
            rampAngleY = ay;
        }
        internal int iChosenArm;
        internal Vector3 rampPosition;
        internal float rampAngleY;
    }

    private static List<Placement> _placements = new List<Placement>();
    private static int _current = -1;

    [Serializable]
    public class PlacementSaved
    {
        public PlacementSaved(string n, Vector3 b, Vector3 p,  float ay, Vector3 hp, Vector3 bp)
        {
            reachingArmName = n;
            reachingArmBase = b;
            rampPosition = p;
            rampAngleY = ay;
            rampHandlePosition = hp;
            rampBallPosition = bp;
        }
        public string reachingArmName;
        public Vector3 reachingArmBase;
        public Vector3 rampPosition;
        public float rampAngleY;
        public Vector3 rampHandlePosition;
        public Vector3 rampBallPosition;
    }

    [Serializable]
    public class PlacementsSaved
    {
        public string rampName;
        public Vector3 rampHandleOffset;
        public Vector3 rampBallOffset;
        public List<PlacementSaved> placements = new List<PlacementSaved>();
    }
}

#endif
