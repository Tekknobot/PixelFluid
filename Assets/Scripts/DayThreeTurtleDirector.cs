using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{

    [DefaultExecutionOrder(15100)]
    [DisallowMultipleComponent]
    public sealed class DayThreeTurtleDirector : MonoBehaviour
    {
        [SerializeField] private Vector2 giantSpawnDelay=new(18f,32f);
        [SerializeField] private Vector2 schoolSpawnDelay=new(10f,22f);
        [SerializeField,Range(2,5)] private int minimumSchoolSize=3;
        [SerializeField,Range(2,6)] private int maximumSchoolSize=5;
        [SerializeField, Min(0.1f)] private float populationCheckInterval = 0.5f;
        private SurfDayProgressionDirector progression; private float nextGiantAt; private float nextSchoolAt; private float nextPopulationCheckAt; private bool active;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Install(){if(FindFirstObjectByType<DayThreeTurtleDirector>()==null)new GameObject("Day 3 Turtle Encounters").AddComponent<DayThreeTurtleDirector>();}
        private void Update()
        {
            if (progression == null)
                progression = FindFirstObjectByType<SurfDayProgressionDirector>();

            bool now = progression != null &&
                       progression.CurrentDay == 3 &&
                       progression.CurrentChapter != SurfDayProgressionDirector.Chapter.Complete;

            if (now && !active) Begin();
            if (!now && active) End();
            if (!now || Time.unscaledTime < nextPopulationCheckAt) return;

            nextPopulationCheckAt = Time.unscaledTime + populationCheckInterval;

            if (Time.unscaledTime >= nextGiantAt &&
                FindFirstObjectByType<GiantTurtleSwimmer>() == null)
            {
                SpawnGiant();
                ScheduleGiant();
            }

            if (Time.unscaledTime >= nextSchoolAt &&
                FindFirstObjectByType<SeaTurtleSwimmer>() == null)
            {
                SpawnSchool();
                ScheduleSchool();
            }
        }
        private void Begin(){active=true;nextPopulationCheckAt=0f;ScheduleGiant(5f);ScheduleSchool(2f);}private void End(){active=false;foreach(var x in FindObjectsByType<GiantTurtleSwimmer>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(x)Destroy(x.gameObject);foreach(var x in FindObjectsByType<SeaTurtleSwimmer>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(x)Destroy(x.gameObject);}
        private void SpawnGiant(){int lanes=Mathf.Max(1,FindObjectsByType<PixelWaterGPU>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Length-1);GameObject go=new("Day 3 Giant Turtle");go.AddComponent<SpriteRenderer>();go.AddComponent<InterWaveRenderItem>();go.AddComponent<Rigidbody2D>();go.AddComponent<BoxCollider2D>();go.AddComponent<GiantTurtleSwimmer>().Initialise(UnityEngine.Random.Range(0,lanes));}
        private void SpawnSchool(){int lanes=Mathf.Max(1,FindObjectsByType<PixelWaterGPU>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Length-1);int lane=UnityEngine.Random.Range(0,lanes);int count=UnityEngine.Random.Range(minimumSchoolSize,maximumSchoolSize+1);float dir=UnityEngine.Random.value<.5f?-1f:1f;Transform leader=null;for(int i=0;i<count;i++){GameObject go=new($"Day 3 Sea Turtle {i+1}");go.AddComponent<SpriteRenderer>();go.AddComponent<InterWaveRenderItem>();go.AddComponent<Rigidbody2D>();go.AddComponent<CircleCollider2D>();Vector2 offset=new(-dir*i*.38f,(i%2==0?1f:-1f)*.12f*Mathf.Ceil(i*.5f));var swimmer=go.AddComponent<SeaTurtleSwimmer>();swimmer.Initialise(Mathf.Clamp(lane+(i==count-1&&count>3?1:0),0,lanes-1),leader,offset,dir);if(i==0)leader=go.transform;}}
        private void ScheduleGiant(float extra=0f)=>nextGiantAt=Time.unscaledTime+extra+UnityEngine.Random.Range(Mathf.Min(giantSpawnDelay.x,giantSpawnDelay.y),Mathf.Max(giantSpawnDelay.x,giantSpawnDelay.y));
        private void ScheduleSchool(float extra=0f)=>nextSchoolAt=Time.unscaledTime+extra+UnityEngine.Random.Range(Mathf.Min(schoolSpawnDelay.x,schoolSpawnDelay.y),Mathf.Max(schoolSpawnDelay.x,schoolSpawnDelay.y));
    }
}
