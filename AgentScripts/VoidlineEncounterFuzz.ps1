$ErrorActionPreference = 'Stop'
$srcPath = 'D:\dump\Voidline\Assets\VOIDLINE\Scripts\Gameplay\VoidlineEncounterDirector.cs'
$director = Get-Content -LiteralPath $srcPath -Raw
$director = $director -replace '^using System;\s*using UnityEngine;\s*', ''
$director = $director -replace 'Hazard second = default;', 'Hazard second = new Hazard();'
$director = $director -replace 'Hazard third = default;', 'Hazard third = new Hazard();'
$stub = @'
using System;
using UnityEngine;
namespace UnityEngine {
    public struct Vector2 {
        public float x, y;
        public Vector2(float x, float y) { this.x=x; this.y=y; }
        public static Vector2 zero { get { return new Vector2(0f,0f); } }
        public static Vector2 positiveInfinity { get { return new Vector2(float.PositiveInfinity,float.PositiveInfinity); } }
        public static bool operator ==(Vector2 a, Vector2 b) { return a.x==b.x && a.y==b.y; }
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a==b); }
        public override bool Equals(object o) { return o is Vector2 && this==(Vector2)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode(); }
    }
    public struct Vector3 {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x; this.y=y; this.z=z; }
    }
    public static class Mathf {
        public static float Clamp01(float x) { return Math.Max(0f,Math.Min(1f,x)); }
        public static float Clamp(float x,float a,float b) { return Math.Max(a,Math.Min(b,x)); }
        public static float Max(float a,float b) { return Math.Max(a,b); }
        public static float Min(float a,float b) { return Math.Min(a,b); }
        public static float Lerp(float a,float b,float t) { return a+(b-a)*t; }
        public static float Abs(float x) { return Math.Abs(x); }
        public static float Sqrt(float x) { return (float)Math.Sqrt(x); }
    }
}
'@
$harness = @'
public static class EncounterFuzzRunner {
 public static string Run(int seeds, int encountersPerSeed) {
  int plans=0, peaks=0, recoveryViolations=0, repeatPeaks=0, outOfBounds=0, unsafeLane=0, badCounts=0, jitterOutOfRange=0, scheduleViolations=0;
  float minStagger=float.PositiveInfinity, maxStagger=0f, sumStagger=0f;
  float minReaction=float.PositiveInfinity, maxReaction=0f, sumReaction=0f;
  float minimumRecoveryGap=float.PositiveInfinity;
  var patternCounts=new System.Collections.Generic.Dictionary<string,int>();
  for(int s=0;s<seeds;s++) {
   var d=new VoidlineEncounterDirector();
   d.BeginRun(unchecked((int)(s*7919+0x5F3759DF)));
   bool mustRecover=false; string prev="Single";
   for(int i=0;i<encountersPerSeed;i++) {
    float elapsed=i*0.78f;
    int passages=i/18;
    int hull=(i%97<6)?1:3;
    float speed=((i/25)%2==0)?12f:25.2f;
    var p=d.Next(elapsed,passages,hull,78f,3.2f,2.2f,8f,18f,speed);
    plans++;
    if(Math.Abs(p.First.Position.z-78f)>2.5001f) jitterOutOfRange++;
    if(!patternCounts.ContainsKey(p.PatternName))patternCounts[p.PatternName]=0;
    patternCounts[p.PatternName]++;
    if(p.Count<1||p.Count>3)badCounts++;
    bool peak=p.PatternName!="Single";
    if(mustRecover&&peak)recoveryViolations++;
    if(peak&&prev!="Single"&&prev==p.PatternName)repeatPeaks++;
    if(peak)peaks++;
    mustRecover=peak;
    prev=p.PatternName;
    var hazards=new[]{p.First,p.Second,p.Third};
    for(int k=0;k<p.Count;k++) {
      var z=hazards[k];
      if(Math.Abs(z.Position.x)>3.2f+0.001f || Math.Abs(z.Position.y)>2.2f+0.001f)outOfBounds++;
      float dx=(z.Position.x-p.SafeLane.x)/1.75f;
      float dy=(z.Position.y-p.SafeLane.y)/1.32f;
      float sep=(float)Math.Sqrt(dx*dx+dy*dy);
      float required=1.05f+Math.Max(0.5f,Math.Min(1.2f,z.Radius))*0.14f;
      // Slalom exposes a different safe lane at each depth: its plan-level safe lane is the entry side.
      if((p.PatternName!="Slalom" || k==0) && sep+0.0001f<required)unsafeLane++;
    }
    if(p.Count>1) {
      float firstGap=Math.Abs(p.Second.Position.z-p.First.Position.z);
      minStagger=Math.Min(minStagger,firstGap);maxStagger=Math.Max(maxStagger,firstGap);sumStagger+=firstGap;
      float firstReaction=firstGap/speed;minReaction=Math.Min(minReaction,firstReaction);maxReaction=Math.Max(maxReaction,firstReaction);sumReaction+=firstReaction;
      if(p.Count>2) {
        float secondGap=Math.Abs(p.Third.Position.z-p.Second.Position.z);
        minStagger=Math.Min(minStagger,secondGap);maxStagger=Math.Max(maxStagger,secondGap);sumStagger+=secondGap;
        float secondReaction=secondGap/speed;minReaction=Math.Min(minReaction,secondReaction);maxReaction=Math.Max(maxReaction,secondReaction);sumReaction+=secondReaction;
      }
      float dz=Math.Abs((p.Count>2?p.Third.Position.z:p.Second.Position.z)-p.First.Position.z);
      const float baseCruiseSpeed=12f, minimumNominalInterval=0.7f;
      float requiredInterval=(dz+5f+1f)/baseCruiseSpeed+0.20f;
      float pacingMultiplier=Math.Max(p.DelayMultiplier,requiredInterval/minimumNominalInterval);
      float travelDistance=baseCruiseSpeed*minimumNominalInterval*pacingMultiplier;
      float clearanceAfterSecond=travelDistance-dz-5f;
      minimumRecoveryGap=Math.Min(minimumRecoveryGap,clearanceAfterSecond);
      if(clearanceAfterSecond+0.0001f<3.4f) scheduleViolations++;
    }
   }
  }
  return "seeds="+seeds+" encountersPerSeed="+encountersPerSeed+" plans="+plans+
    " peakPlans="+peaks+" recoveryViolations="+recoveryViolations+" repeatedPeaks="+repeatPeaks+
    " boundsViolations="+outOfBounds+" safeLaneViolations="+unsafeLane+" badCounts="+badCounts+
    " jitterOutOfRange="+jitterOutOfRange+" scheduleViolations="+scheduleViolations+
    " minNextFormationClearance="+minimumRecoveryGap.ToString("F2")+
    " pairStaggerMin="+minStagger.ToString("F2")+" max="+maxStagger.ToString("F2")+
    " avg="+(sumStagger/Math.Max(1,peaks)).ToString("F2")+
    " reactionMin="+minReaction.ToString("F2")+" max="+maxReaction.ToString("F2")+
    " avg="+(sumReaction/Math.Max(1,peaks)).ToString("F2")+
    " patterns="+string.Join(",",System.Linq.Enumerable.Select(patternCounts,x=>x.Key+":"+x.Value));
 }
}
'@
$complete = [string]::Join([Environment]::NewLine, @($stub,$director,$harness))
Add-Type -TypeDefinition $complete -Language CSharp
[EncounterFuzzRunner]::Run(1000, 500)
