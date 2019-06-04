using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRLoader.Modules;
using VRLoader.Attributes;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Reflection;
using System.Collections;
using VRCTools;
using VRCSDK2;
using VRC;

namespace BlizzieVRC
{

    [ModuleInfo("Blizzie VRCMod", "1.0", "Blizzie")]
    public class BlizzieVRCMod : VRModule
    {

        private static Texture2D ConsoleTextures;
        private Vector2 ConsoleScrollPos = Vector2.zero;
        private static List<string> ConsoleLogs = new List<string>();
        private List<string> PlayersInRoom = new List<string>();

        private Transform ToggleLog;
        private Transform ClearLog;
        private Transform ToggleTP;
        private Transform AddJump;
        private Transform ToggleGravity;
        private Transform ToggleNoclip;
        private GUIStyle gUIStyle = GUIStyle.none;
        private GUIStyle gUIStyle2 = GUIStyle.none;
        private bool isGUIReady = false;
        private bool showLog = false;
        private bool isReady = false;
        private bool wasInRoom = false;
        private static bool currentlyInRoom = false;

        private Vector3 originalGravity;
        private bool isNoGravity = false;
        private bool isNoclip = false;

        private GameObject TPCamera;
        private GameObject OriginalCamera;
        private float HeadDistance;
        private bool isThirdPerson = false;

        public float targetHeight = 0.2f;
        public float distance = 5.0f;
        public float offsetFromWall = 0.1f;
        public float maxDistance = 10;
        public float minDistance = 0.6f;
        public float speedDistance = 5;

        public LayerMask collisionLayers = -1;
        
        public int zoomRate = 20;

        public float zoomDampening = 5.0f;

        private float currentDistance = 2f;
        private float desiredDistance = 2f;
        private float correctedDistance = 2f;

        private static List<int> noClipToEnable = new List<int>();

        // -1050 1470
        // 420x420

        private IEnumerator Setup()
        {
            isReady = false;
            while (!isReady)
            {
                yield return VRCUiManagerUtils.WaitForUiManagerInit();
                isReady = true;
            }
            Transform ButtonBase = QuickMenuUtils.GetQuickMenuInstance().transform.Find("ShortcutMenu/SettingsButton");
            if (ButtonBase != null)
            {
                // Log window button
                ToggleLog = UnityUiUtils.DuplicateButton(ButtonBase, "Toggle\nConsole", new Vector2(0f, 0f));
                ToggleLog.name = "ToggleLogButton";
                ToggleLog.GetComponent<Button>().onClick.RemoveAllListeners();
                ToggleLog.GetComponent<Button>().onClick.AddListener(delegate
                {
                    ToggleLogWindow();
                });
                ToggleLog.GetComponent<RectTransform>().SetParent(ButtonBase.parent, true);
                ToggleLog.GetComponent<RectTransform>().localPosition = new Vector3(-1050f, 1470f, 0f);
                ToggleLog.GetComponent<RectTransform>().localRotation = Quaternion.identity;

                // Clear Log window button
                ClearLog = UnityUiUtils.DuplicateButton(ButtonBase, "Clear\nConsole", new Vector2(0f, 0f));
                ClearLog.name = "ClearLogButton";
                ClearLog.GetComponent<Button>().onClick.RemoveAllListeners();
                ClearLog.GetComponent<Button>().onClick.AddListener(delegate
                {
                    ConsoleLogs.Clear();
                });
                ClearLog.GetComponent<RectTransform>().SetParent(ButtonBase.parent, true);
                ClearLog.GetComponent<RectTransform>().localPosition = new Vector3(-1050f, 1050f, 0f);
                ClearLog.GetComponent<RectTransform>().localRotation = Quaternion.identity;

                // Third person button
                ToggleTP = UnityUiUtils.DuplicateButton(ButtonBase, "Toggle\nThird\nPerson", new Vector2(0f, 0f));
                ToggleTP.name = "ToggleTPButton";
                ToggleTP.GetComponent<Button>().onClick.RemoveAllListeners();
                ToggleTP.GetComponent<Button>().onClick.AddListener(delegate
                {
                    ToggleThirdPerson();
                });
                ToggleTP.GetComponent<RectTransform>().SetParent(ButtonBase.parent, true);
                ToggleTP.GetComponent<RectTransform>().localPosition = new Vector3(-1050f, 1890f, 0f);
                ToggleTP.GetComponent<RectTransform>().localRotation = Quaternion.identity;

                // Enable jumping
                AddJump = UnityUiUtils.DuplicateButton(ButtonBase, "Enable\nJumping", new Vector2(0f, 0f));
                AddJump.name = "ToggleJumpButton";
                AddJump.GetComponent<Button>().onClick.RemoveAllListeners();
                AddJump.GetComponent<Button>().onClick.AddListener(delegate
                {
                    EnableJumping();
                });
                AddJump.GetComponent<RectTransform>().SetParent(ButtonBase.parent, true);
                AddJump.GetComponent<RectTransform>().localPosition = new Vector3(1050f, 1890f, 0f);
                AddJump.GetComponent<RectTransform>().localRotation = Quaternion.identity;

                // Enable no gravity
                ToggleGravity = UnityUiUtils.DuplicateButton(ButtonBase, "Toggle\nGravity", new Vector2(0f, 0f));
                ToggleGravity.name = "ToggleGravityButton";
                ToggleGravity.GetComponent<Button>().onClick.RemoveAllListeners();
                ToggleGravity.GetComponent<Button>().onClick.AddListener(delegate
                {
                    toggleGravity();
                });
                ToggleGravity.GetComponent<RectTransform>().SetParent(ButtonBase.parent, true);
                ToggleGravity.GetComponent<RectTransform>().localPosition = new Vector3(1050f, 630f, 0f);
                ToggleGravity.GetComponent<RectTransform>().localRotation = Quaternion.identity;

                // Enable noclip
                ToggleNoclip = UnityUiUtils.DuplicateButton(ButtonBase, "Toggle\nNoclip", new Vector2(0f, 0f));
                ToggleNoclip.name = "ToggleNoclipButton";
                ToggleNoclip.GetComponent<Button>().onClick.RemoveAllListeners();
                ToggleNoclip.GetComponent<Button>().onClick.AddListener(delegate
                {
                    toggleNoclip();
                });
                ToggleNoclip.GetComponent<RectTransform>().SetParent(ButtonBase.parent, true);
                ToggleNoclip.GetComponent<RectTransform>().localPosition = new Vector3(-1050f, 630f, 0f);
                ToggleNoclip.GetComponent<RectTransform>().localRotation = Quaternion.identity;

            }
            else
            {
                AddDebugLine("Failed to find Button Base");
            }
            InvokeRepeating("WatchForPlayers", 0f, 5f);

        }

        private void EnableJumping()
        {
            if(VRCPlayer.Instance.gameObject.GetComponent<PlayerModComponentJump>() == null)
            {
                VRCPlayer.Instance.gameObject.AddComponent<PlayerModComponentJump>();
                AddDebugLine("Enabled jumping!");
            }
        }

        private void toggleGravity()
        {
            isNoGravity = !isNoGravity;
            if(isNoGravity)
            {
                originalGravity = Physics.gravity;
                Physics.gravity = Vector3.zero;
            }
            else
            {
                Physics.gravity = originalGravity;
            }
        }

        private void WatchForPlayers()
        {

            currentlyInRoom = RoomManagerBase.PNBKNEPHGNG;
            if(!currentlyInRoom && wasInRoom)
            {
                PlayersInRoom.Clear();
                wasInRoom = false;
            }
            else if(currentlyInRoom && !wasInRoom)
            {
                VRC.Player[] allPlayers = PlayerManager.GetAllPlayers();
                for(int i = 0; i < allPlayers.Length; i++)
                {
                    VRC.Player P = allPlayers[i];
                    try
                    {
                        PlayersInRoom.Add(P.PAGDFJMFIBP.displayName);
                    }
                    catch
                    {

                    }
                }

                wasInRoom = true;
            }
            else if(currentlyInRoom && wasInRoom)
            {
                VRC.Player[] allPlayers = PlayerManager.GetAllPlayers();
                if (allPlayers.Length > PlayersInRoom.Count)
                {
                    foreach (VRC.Player P in allPlayers)
                    {
                        VRC.Core.APIUser AP = P.PAGDFJMFIBP;
                        if(!PlayersInRoom.Any(x => x == AP.displayName))
                        {
                            PlayersInRoom.Add(AP.displayName);
                            AddDebugLine("Player Joined: " + AP.displayName);
                        }
                    }
                }
                else if(allPlayers.Length < PlayersInRoom.Count)
                {
                    foreach(string S in PlayersInRoom)
                    {
                        if(!allPlayers.Any(x => x.PAGDFJMFIBP.displayName == S))
                        {
                            PlayersInRoom.Remove(S);
                            AddDebugLine("Player Left: " + S);
                        }
                    }
                }
            }
        }

        private static float GetPrivateFloat<T>(T obj, string propertyName)
        {
            foreach (FieldInfo fi in obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (fi.Name.ToLower().Contains(propertyName.ToLower()))
                {
                    return (float)fi.GetValue(obj);
                }
            }
            return -1f;
        }

        private static void SetPrivatePropertyValue<T>(T obj, string propertyName, object newValue)
        {
            foreach (FieldInfo fi in obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (fi.Name.ToLower().Contains(propertyName.ToLower()))
                {
                    fi.SetValue(obj, newValue);
                    break;
                }
            }
        }

        private void ToggleThirdPerson()
        {
            if(OriginalCamera == null)
            {
                OriginalCamera = GameObject.Find("Camera (eye)");
                HeadDistance = GetPrivateFloat(VRCPlayer.Instance.JFBFGHJHEOL, "OACDEFBDFPP");
            }
            if(TPCamera == null && OriginalCamera != null)
            {
                TPCamera = new GameObject();
                TPCamera.transform.localScale = OriginalCamera.transform.localScale;
                Rigidbody RB = TPCamera.AddComponent<Rigidbody>();
                RB.isKinematic = true;
                RB.useGravity = false;
                Camera CM = TPCamera.AddComponent<Camera>();
                CM.fieldOfView = 75f;
                CM.cullingMask = OriginalCamera.GetComponent<Camera>().cullingMask;
                CM.enabled = false;

                TPCamera.transform.parent = OriginalCamera.transform;
                TPCamera.transform.position = OriginalCamera.transform.position;
                TPCamera.transform.rotation = OriginalCamera.transform.rotation;
                TPCamera.transform.position = (TPCamera.transform.position - TPCamera.transform.forward * 1.1f);

            }

            if(TPCamera != null)
            {
                isThirdPerson = !isThirdPerson;
                OriginalCamera.GetComponent<Camera>().enabled = !isThirdPerson;
                TPCamera.GetComponent<Camera>().enabled = isThirdPerson;
                if (isThirdPerson)
                    SetPrivatePropertyValue(VRCPlayer.Instance.JFBFGHJHEOL, "OACDEFBDFPP", 0.0001f);
                else
                    SetPrivatePropertyValue(VRCPlayer.Instance.JFBFGHJHEOL, "OACDEFBDFPP", HeadDistance);
            }


        }


        public void LateUpdate()
        {
            if(isThirdPerson && TPCamera != null && OriginalCamera != null)
            {
                Vector3 vTargetOffset;
                desiredDistance -= Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime * zoomRate * Mathf.Abs(desiredDistance) * speedDistance;
                desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
                correctedDistance = desiredDistance;
                vTargetOffset = new Vector3(0, -targetHeight, 0);
                Vector3 position = OriginalCamera.transform.position - (OriginalCamera.transform.rotation * Vector3.forward * desiredDistance + vTargetOffset);
                RaycastHit collisionHit;
                Vector3 trueTargetPosition = new Vector3(OriginalCamera.transform.position.x, OriginalCamera.transform.position.y, OriginalCamera.transform.position.z) - vTargetOffset;
                bool isCorrected = false;
                if (Physics.Linecast(trueTargetPosition, position, out collisionHit, collisionLayers.value))
                {
                    correctedDistance = Vector3.Distance(trueTargetPosition, collisionHit.point) - offsetFromWall;
                    isCorrected = true;
                }
                currentDistance = !isCorrected || correctedDistance > currentDistance ? Mathf.Lerp(currentDistance, correctedDistance, Time.deltaTime * zoomDampening) : correctedDistance;
                currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
                position = OriginalCamera.transform.position - (OriginalCamera.transform.rotation * Vector3.forward * currentDistance + vTargetOffset);
                TPCamera.transform.position = position;
            }
        }

        private static float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360)
                angle += 360;
            if (angle > 360)
                angle -= 360;
            return Mathf.Clamp(angle, min, max);
        }

        private Transform FindChildByName(string ThisName, Transform ThisGObj)
        {
            Transform ReturnObj;
            if (ThisGObj.name == ThisName)
                return ThisGObj.transform;
            foreach (Transform child in ThisGObj)
            {
                ReturnObj = FindChildByName(ThisName, child);
                if (ReturnObj)
                    return ReturnObj;
            }
            return null;
        }

        public void toggleNoclip()
        {
            isNoclip = !isNoclip;
            Collider[] array = GameObject.FindObjectsOfType<Collider>();
            Component component = VRCPlayer.Instance.GetComponents(typeof(Collider)).FirstOrDefault<Component>();
            Collider[] array2 = array;
            for (int i = 0; i < array2.Length; i++)
            {
                Collider collider = array2[i];
                bool flag = collider.GetComponent<PlayerSelector>() != null || collider.GetComponent<VRC_Pickup>() != null || collider.GetComponent<QuickMenu>() != null || collider.GetComponent<VRC_Station>() != null || collider.GetComponent<VRC_AvatarPedestal>() != null;
                if (flag)
                {
                    collider.enabled = true;
                }
                else
                {
                    bool flag2 = collider != component && ((isNoclip && collider.enabled || (!isNoclip && noClipToEnable.Contains(collider.GetInstanceID()))));
                    if (flag2)
                    {
                        collider.enabled = !isNoclip;
                        if (isNoclip)
                        {
                            noClipToEnable.Add(collider.GetInstanceID());
                        }
                    }
                }
            }
            bool flag3 = !isNoclip;
            if (flag3)
            {
                noClipToEnable.Clear();
            }
        }

        public void ToggleLogWindow()
        {
            showLog = !showLog;
        }

        public void Update()
        {

            if(isReady)
            {
                if(Input.GetKey(KeyCode.UpArrow))
                {
                    ConsoleScrollPos.y += 3;
                }
                if (Input.GetKey(KeyCode.DownArrow))
                {
                    ConsoleScrollPos.y -= 3;
                }
                if(Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    targetHeight -= 0.1f;
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    targetHeight += 0.1f;
                }
                if(Input.GetKey(KeyCode.Q) && isNoGravity)
                {
                    float Speed = Input.GetKey(KeyCode.LeftShift) ? 20f : 2f;
                    VRCPlayer.Instance.gameObject.transform.position = VRCPlayer.Instance.gameObject.transform.position + new Vector3(0f, Speed * Time.deltaTime, 0f);
                }
                if (Input.GetKey(KeyCode.E) && isNoGravity)
                {
                    float Speed = Input.GetKey(KeyCode.LeftShift) ? 20f : 2f;
                    VRCPlayer.Instance.gameObject.transform.position = VRCPlayer.Instance.gameObject.transform.position - new Vector3(0f, Speed * Time.deltaTime, 0f);
                }
                if(Input.GetKeyDown(KeyCode.G))
                {
                    toggleGravity();
                }
                if (Input.GetKeyDown(KeyCode.N))
                {
                    toggleNoclip();
                }
            }
        }

        public void Start()
        {
            StartCoroutine(Setup());
        }

        public void OnGUI()
        {
            if(!isGUIReady)
            {

                gUIStyle = new GUIStyle(GUI.skin.box);
                gUIStyle2 = new GUIStyle(GUI.skin.box);
                gUIStyle.alignment = TextAnchor.UpperRight;
                gUIStyle2.alignment = TextAnchor.UpperRight;
                gUIStyle.wordWrap = true;
                bool hasTextures = ConsoleTextures != null;
                if (hasTextures)
                {
                    gUIStyle.normal.background = ConsoleTextures;
                    gUIStyle2.normal.background = ConsoleTextures;
                }
                else
                {
                    ConsoleTextures = new Texture2D(1, 1);
                    ConsoleTextures.SetPixels(new Color[]
                    {
                    new Color(0.45f, 0.45f, 0.45f, 0.45f)
                    });
                    ConsoleTextures.Apply();
                }
                isGUIReady = true;
            }
            if (showLog)
            {
                Rect position = new Rect((float)Screen.width * 0.7f, (float)Screen.height * 0.63f, (float)Screen.width * 0.29f, (float)Screen.height * 0.31f);
                Rect screenRect = new Rect((float)Screen.width * 0.705f, (float)Screen.height * 0.66f, (float)Screen.width * 0.28f, (float)Screen.height * 0.27f);
                GUI.Box(position, "<b>Console</b>", gUIStyle2);
                GUILayout.BeginArea(screenRect);
                bool hasLogs = ConsoleLogs.Count > 0;
                if (hasLogs)
                {
                    ConsoleScrollPos = GUILayout.BeginScrollView(this.ConsoleScrollPos, new GUILayoutOption[0]);
                    int num = 1;
                    bool hasOverflow = ConsoleLogs.Count > 30;
                    if (hasOverflow)
                    {
                        num = ConsoleLogs.Count - 30;
                        for (int i = num; i < ConsoleLogs.Count; i++)
                        {
                            string arg = ConsoleLogs[num];
                            GUILayout.Box(string.Format("{0}", arg), gUIStyle, new GUILayoutOption[]
                            {
                                GUILayout.MaxWidth((float)Screen.width * 0.9f)
                            });
                            num++;
                        }
                    }
                    else
                    {
                        foreach (string current in ConsoleLogs)
                        {
                            GUILayout.Box(string.Format("{0}", current), gUIStyle, new GUILayoutOption[]
                            {
                                GUILayout.MaxWidth((float)Screen.width * 0.9f)
                            });
                            num++;
                        }
                    }
                    GUILayout.EndScrollView();
                }
                GUILayout.EndArea();
            }
        }

        public void AddDebugLine(string line)
        {
            ConsoleLogs.Add("<b>" + line + "</b>");
        }
    }
}

