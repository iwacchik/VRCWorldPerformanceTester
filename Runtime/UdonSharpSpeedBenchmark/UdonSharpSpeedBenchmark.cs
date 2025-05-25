using System;
using System.Text;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UdonSharpEditor;
using UnityEditorInternal;
using System.Linq;
#endif

namespace IwacchiLab.PerfomanceTester
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class UdonSharpSpeedBenchmark : UdonSharpBehaviour
    {
        [SerializeField, HideInInspector] private UdonSharpSpeedBenchmarkRunner[] _runners;
        [SerializeField, HideInInspector] private string[] _taskNames;
        [SerializeField, HideInInspector] private int[] _iterations;

        private bool _isExecuting;
        private int _taskIndex = 0;

        [SerializeField] private int _testRunCount = 10;
        private int _runCount = 0;
        private int _runFrame;
        private string _runningTask = string.Empty;
        
        // リザルト
        private DataDictionary _results = new DataDictionary();
        [SerializeField] private TMP_Text _realtimeLog;
        [SerializeField] private TMP_InputField _resultsText;


        private void LateUpdate()
        {
            if (!string.IsNullOrEmpty(_runningTask) && Time.frameCount - _runFrame >= 2)
            {
                // ここに到達した時点でFailed
                FailTask();
            }
        }
        
        public void RunBenchmark()
        {
            if (_isExecuting || _runners.Length == 0)
            {
                return;
            }

            _isExecuting = true;
            _taskIndex = 0;
            _runCount = 0;
            _results.Clear();
            _NextRun();
        }

        public void CompleteTask(string methodName, int count, double resultMilliseconds, int iterations)
        {
            Debug.Log($"==== CompleteMethod {_runningTask}[{_runCount}] ====");
            SetResult(true, _runningTask, resultMilliseconds);
            _runningTask = string.Empty;

            SendCustomEventDelayedFrames(nameof(_NextRun), 5);
        }

        private void FailTask()
        {
            Debug.Log($"==== FailMethod {_runningTask}[{_runCount}] ====");
            SetResult(false, _runningTask, 0);
            _runningTask = string.Empty;

            SendCustomEventDelayedFrames(nameof(_NextRun), 5);
        }
        
        public void _NextRun()
        {
            if (_runCount >= _testRunCount)
            {
                _taskIndex++;
                _runCount = 0;
            }

            if (_taskIndex >= _runners.Length)
            {
                // 終了
                _isExecuting = false;
                
                // 最終ログ出力
                OutputAllResult();
                return;
            }

            _runCount++;
            // var data = _benchmarkData[_taskIndex].DataDictionary;
            var prefab = _runners[_taskIndex].gameObject;
            var task = _taskNames[_taskIndex];
            var iterations = _iterations[_taskIndex];

            var runner = Instantiate(prefab).GetComponent<UdonSharpSpeedBenchmarkRunner>();
            runner.RunBenchmark(task, _runCount, iterations, (IUdonEventReceiver)this);
            _runFrame = Time.frameCount;
            _runningTask = $"{task}_{iterations}";
            Debug.Log($"==== Start Method {_runningTask}[{_runCount}] ====");
        }

        private void SetResult(bool isSuccess, string taskName, double resultMilliseconds)
        {
            var result = GetResult($"{taskName}");

            result["RunCount"] = result["RunCount"].Int + 1;
            result["SuccessCount"] = result["SuccessCount"].Int + (isSuccess ? 1 : 0);
            result["ElapsedTotal"] = result["ElapsedTotal"].Double + (isSuccess ? resultMilliseconds : 0);
            result["ElapsedMax"] =
                Math.Max(result["ElapsedMax"].Double, (isSuccess ? resultMilliseconds : double.MinValue));
            result["ElapsedMin"] =
                Math.Min(result["ElapsedMin"].Double, (isSuccess ? resultMilliseconds : double.MaxValue));

            if (!result.TryGetValue("ElapsedList", out var list))
            {
                list = result["ElapsedList"] = new DataList();
            }
            list.DataList.Add(resultMilliseconds);

            // リアルタイムログ出力
            if (Utilities.IsValid(_realtimeLog))
            {
                _realtimeLog.text = OutputResult(taskName, result);
            }
        }

        private DataDictionary GetResult(string methodName)
        {
            var containsKey = _results.ContainsKey(methodName);
            if (!containsKey)
            {
                var dic = new DataDictionary();
                dic["ElapsedTotal"] = 0.0;
                dic["ElapsedMax"] = double.MinValue;
                dic["ElapsedMin"] = double.MaxValue;
                dic["RunCount"] = 0;
                dic["SuccessCount"] = 0;

                _results.Add(methodName, dic);
            }

            return _results[methodName].DataDictionary;
        }

        private void OutputAllResult()
        {
            var sb = new StringBuilder();

            var keyList = _results.GetKeys();
            for (var i = 0; i < keyList.Count; i++)
            {
                var key = keyList[i];
                var result = _results[key].DataDictionary;
                
                sb.AppendLine(
                    OutputResult(key.String, result)
                    );
            }

            Debug.Log(sb.ToString());

            if (Utilities.IsValid(_resultsText))
            {
                _resultsText.text = sb.ToString();
            }

        }

        private string OutputResult( string name, DataDictionary result)
        {
            var runCount = result["RunCount"].Int;
            var successCount = result["SuccessCount"].Int;
            var countText = $"[{successCount}/{runCount}]";
            var aveText = successCount != 0 ? $"{result["ElapsedTotal"].Double / runCount:F3}ms" : "===";
            var minText = successCount != 0 ? $"{result["ElapsedMin"].Double:F3}ms" : "===";
            var maxText = successCount != 0 ? $"{result["ElapsedMax"].Double:F3}ms" : "===";
            var median = successCount != 0 ? $"{CalculateMedian(result["ElapsedList"].DataList)}ms" : "===";
            
            return $"{name} {countText} Median:{median} Min:{minText} Max:{maxText}";
        }
        
        public double CalculateMedian(DataList list)
        {
            list.Sort();
            
            int length = list.Count;
            // 奇数の場合は中央の値を返す
            if (length % 2 == 1)
            {
                return list[length / 2].Double;
            }
            // 偶数の場合は中央2つの値の平均を返す
            else
            {
                int middleIndex = length / 2;
                return (list[middleIndex - 1].Double + list[middleIndex].Double) / 2f;
            }
        }
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    [CustomEditor(typeof(UdonSharpSpeedBenchmark))]
    public class UdonSharpSpeedBenchmarkEditor : Editor
    {
        [Serializable]
        public class SerializedRunnerParam : ScriptableObject
        {
            public UdonSharpSpeedBenchmarkRunner Runner;
            public string TaskName;
            public int Iterations;
        }

        private class RunnerParamEditorSlot
        {
            public SerializedRunnerParam RunnerParam;
            private SerializedObject _serializedObject;

            public RunnerParamEditorSlot(
                UdonSharpSpeedBenchmarkRunner runner = null,
                string taskName = "",
                int iterations = 0
            )
            {
                RunnerParam = CreateInstance<SerializedRunnerParam>();
                RunnerParam.Runner = runner;
                RunnerParam.TaskName = taskName;
                RunnerParam.Iterations = iterations;

                _serializedObject = new SerializedObject(RunnerParam);
            }

            public void Update()
            {
                _serializedObject.Update();
            }

            public void Apply()
            {
                _serializedObject.ApplyModifiedProperties();
            }

            public void Dispose()
            {
                if (RunnerParam != null)
                {
                    DestroyImmediate(RunnerParam);
                    RunnerParam = null;
                    _serializedObject = null;
                }
            }

            public SerializedProperty GetProperty(string propertyName) =>
                _serializedObject.FindProperty(propertyName);

            // public float GetPropertyHeight( string propertyName )
            // {
            //     return EditorGUI.GetPropertyHeight(GetProperty(propertyName), true);
            // }
        }

        private readonly List<RunnerParamEditorSlot> _paramEditorSlots = new List<RunnerParamEditorSlot>();
        private ReorderableList _reorderableList;

        private SerializedProperty _runnersProp;
        private SerializedProperty _taskNamesProp;
        private SerializedProperty _iterationsProp;

        private void OnEnable()
        {
            FindProperties();
            RefreshFromBehaviorSerialized();

            _reorderableList = new ReorderableList(_paramEditorSlots, typeof(RunnerParamEditorSlot), true, true,
                true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Tests"),
                elementHeightCallback = index =>
                {
                    // float height = EditorGUIUtility.singleLineHeight * 2 + 12;
                    // // height += _paramEditor[index].GetPropertyHeight();
                    // return height;
                    return EditorGUIUtility.singleLineHeight + 2;
                },
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var editorSlot = _paramEditorSlots[index];
                    editorSlot.Update();

                    // プロパティ取得
                    var runnerProp = editorSlot.GetProperty("Runner");
                    var taskNameProp = editorSlot.GetProperty("TaskName");
                    var iterationsProp = editorSlot.GetProperty("Iterations");

                    //Rect領域設定
                    float lineHeight = EditorGUIUtility.singleLineHeight;
                    float spacing = 4f;
                    float y = rect.y + 2f;
                    float x = rect.x;
                    float fullWidth = rect.width;

                    // 横幅を3分割（Runner, TaskName, Iterations）
                    float runnerWidth = fullWidth * 0.4f;
                    float taskNameWidth = fullWidth * 0.4f;
                    float iterationsWidth = fullWidth * 0.2f;


                    // 子オブジェクトリスト
                    var runners = ((UdonSharpSpeedBenchmark)target)
                        .GetComponentsInChildren<UdonSharpSpeedBenchmarkRunner>();
                    var runnerNames = runners.Select(r => r.GetType().Name).ToArray();
                    var current = (UdonSharpSpeedBenchmarkRunner)runnerProp.boxedValue;
                    var selectedRunnerIndex = Array.FindIndex(runners, r => r == current);
                    if (selectedRunnerIndex < 0)
                    {
                        selectedRunnerIndex = 0;
                    }

                    var newIndex = EditorGUI.Popup(
                        new Rect(x, y, runnerWidth - spacing, lineHeight),
                        selectedRunnerIndex,
                        runnerNames
                    );
                    x += runnerWidth;

                    var methodNames = Array.Empty<string>();
                    var newMethodIndex = 0;
                    if (runners.Length > 0)
                    {
                        methodNames = GetPublicMethodNames(runners[selectedRunnerIndex]);
                        var currentMethod = taskNameProp.stringValue;
                        var selectedMethodIndex = Mathf.Max(Array.FindIndex(methodNames, m => m == currentMethod), 0);

                        newMethodIndex = EditorGUI.Popup(
                            new Rect(x, y, runnerWidth - spacing, lineHeight),
                            selectedMethodIndex,
                            methodNames
                        );
                        x += taskNameWidth;

                        EditorGUI.PropertyField(
                            new Rect(x, y, iterationsWidth, lineHeight),
                            iterationsProp,
                            GUIContent.none
                        );

                    }

                    if (GUI.changed)
                    {
                        runnerProp.boxedValue = runners.Length == 0 ? null : runners[newIndex];
                        taskNameProp.stringValue = methodNames.Length == 0 ? "" : methodNames[newMethodIndex];

                        editorSlot.Apply();
                    }
                },
                onAddCallback = list =>
                {
                    Undo.RecordObject(target, "Add Param");
                    _paramEditorSlots.Add(new RunnerParamEditorSlot());
                },
                onRemoveCallback = list =>
                {
                    if (list.index >= 0 && list.index < _paramEditorSlots.Count)
                    {
                        Undo.RecordObject(target, "Remove Param");
                        _paramEditorSlots[list.index].Dispose();
                        _paramEditorSlots.RemoveAt(list.index);
                    }
                }
            };
        }

        private void OnDisable()
        {
            foreach (var e in _paramEditorSlots)
            {
                e.Dispose();
            }

            _paramEditorSlots.Clear();
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();

            DrawAllSerializedFieldsExceptScript();
            EditorGUILayout.Space(4);

            if (Event.current.type == EventType.Layout)
            {
                RefreshFromBehaviorSerialized();
            }

            EditorGUI.BeginChangeCheck();
            _reorderableList.DoLayoutList();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modify Parameters");
                ApplyBackToBehaviorSerialized();
                EditorUtility.SetDirty(target);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAllSerializedFieldsExceptScript()
        {
            var iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script") continue;

                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        private void FindProperties()
        {
            _runnersProp = serializedObject.FindProperty("_runners");
            _taskNamesProp = serializedObject.FindProperty("_taskNames");
            _iterationsProp = serializedObject.FindProperty("_iterations");
        }

        private void RefreshFromBehaviorSerialized()
        {
            _paramEditorSlots.Clear();

            var arraySize = _runnersProp.arraySize;
            _taskNamesProp.arraySize = arraySize;
            _iterationsProp.arraySize = arraySize;
            for (int i = 0; i < arraySize; i++)
            {
                _paramEditorSlots.Add(new RunnerParamEditorSlot(
                    runner: (UdonSharpSpeedBenchmarkRunner)_runnersProp.GetArrayElementAtIndex(i).boxedValue,
                    taskName: _taskNamesProp.GetArrayElementAtIndex(i).stringValue,
                    iterations: _iterationsProp.GetArrayElementAtIndex(i).intValue
                ));
            }
        }

        private void ApplyBackToBehaviorSerialized()
        {
            var arraySize = _paramEditorSlots.Count;
            _runnersProp.arraySize = arraySize;
            _taskNamesProp.arraySize = arraySize;
            _iterationsProp.arraySize = arraySize;

            for (int i = 0; i < arraySize; i++)
            {
                var runnerParam = _paramEditorSlots[i].RunnerParam;
                _runnersProp.GetArrayElementAtIndex(i).boxedValue = runnerParam.Runner;
                _taskNamesProp.GetArrayElementAtIndex(i).stringValue = runnerParam.TaskName;
                _iterationsProp.GetArrayElementAtIndex(i).intValue = runnerParam.Iterations;
            }
        }

        // publicなインスタンスメソッド名を取得
        private static string[] GetPublicMethodNames(UdonSharpSpeedBenchmarkRunner runner)
        {
            if (runner == null) return new string[] { };

            return runner
                .GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m =>
                        !m.IsSpecialName && // プロパティの getter/setter を除外
                        m.GetParameters().Length == 0 // 引数なしメソッドだけに限定
                )
                .Select(m => m.Name)
                .ToArray();
        }
    }
#endif
}