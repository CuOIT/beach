using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaveLab
{
    public sealed partial class WaveLabController
    {
        const float SimulationStep=1f/60f;
        const int CheckpointInterval=30, MaximumCheckpoints=256;
        readonly Dictionary<int,Checkpoint> seekCheckpoints=new Dictionary<int,Checkpoint>();
        int seekSignature,seekStep,seekTargetStep;
        float seekRemainder;
        public bool IsSeeking {get;private set;}

        sealed class Checkpoint
        {
            public float clock;
            public BodyState[] bodies;
        }
        struct BodyState
        {
            public Vector2 position,velocity;
            public float rotation,spin;
        }

        // UI seeks are coalesced and processed within a small per-frame budget.
        public void RequestSeek(float seconds)
        {
            paused=true;
            BeginSeek(seconds);
        }
        void CancelSeek()
        {
            IsSeeking=false;
            seekCheckpoints.Clear();
        }
        int SimulationSignature()
        {
            int hash=17;
            void Add(float value){unchecked{hash=hash*31+value.GetHashCode();}}
            Add(period);Add(runupReach);Add(currentStrength);Add(approachTime);Add(advanceTime);
            Add(holdTime);Add(recedeTime);Add(restTime);Add(lateralDrift);Add(breakerPush);Add(swashPush);
            Add(acceleration);Add(settleDamping);Add(rockSteering);Add(objectSteering);Add(turnStrength);Add(turnLimit);
            Add(moveObjects?1:0);Add(seed);Add(bodies.Count);
            foreach(var b in bodies)
            {
                Add(b.initial.x);Add(b.initial.y);Add(b.initialRotation);Add(b.radius);Add(b.buoyancy);Add(b.bobSeed);
            }
            foreach(var r in rocks){Add(r.x);Add(r.y);Add(r.z);Add(r.w);}
            return hash;
        }
        void BeginSeek(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
            seconds=Mathf.Max(0,seconds);
            int signature=SimulationSignature();
            int targetStep=Mathf.FloorToInt(seconds*60);
            bool continueForward=IsSeeking&&signature==seekSignature&&targetStep>=seekStep;
            if(signature!=seekSignature){seekCheckpoints.Clear();seekSignature=signature;}
            dragged=null;touchPointer=false;activePointerId=int.MinValue;accumulator=0;
            if(!continueForward)
            {
                int candidate=Math.Min(targetStep/CheckpointInterval,MaximumCheckpoints-1)*CheckpointInterval;
                while(candidate>0&&!seekCheckpoints.ContainsKey(candidate))candidate-=CheckpointInterval;
                if(seekCheckpoints.TryGetValue(candidate,out var checkpoint))RestoreCheckpoint(checkpoint);
                else{ResetState();candidate=0;SaveCheckpoint(0);}
                seekStep=candidate;
            }
            seekTargetStep=targetStep;
            seekRemainder=seconds-targetStep/60f;
            IsSeeking=true;
        }
        void AdvanceSeek(int maxSteps,double milliseconds)
        {
            // Live tuning during a queued seek invalidates the previous trajectory.
            if(SimulationSignature()!=seekSignature)
                BeginSeek(seekTargetStep/60f+seekRemainder);
            var timer=System.Diagnostics.Stopwatch.StartNew();
            int count=0;
            while(seekStep<seekTargetStep&&count<maxSteps)
            {
                Step(SimulationStep);seekStep++;count++;
                if(seekStep%CheckpointInterval==0&&seekStep/CheckpointInterval<MaximumCheckpoints)SaveCheckpoint(seekStep);
                if(milliseconds>0&&timer.Elapsed.TotalMilliseconds>=milliseconds)break;
            }
            if(seekStep==seekTargetStep)
            {
                if(seekRemainder>0)Step(seekRemainder);
                IsSeeking=false;
            }
            ApplyGlobals();
        }
        void SaveCheckpoint(int step)
        {
            if(seekCheckpoints.ContainsKey(step))return;
            var checkpoint=new Checkpoint{clock=clock,bodies=new BodyState[bodies.Count]};
            for(int i=0;i<bodies.Count;i++)
            {
                var b=bodies[i];
                checkpoint.bodies[i]=new BodyState{position=b.position,velocity=b.velocity,rotation=b.rotation,spin=b.spin};
            }
            seekCheckpoints.Add(step,checkpoint);
        }
        void RestoreCheckpoint(Checkpoint checkpoint)
        {
            clock=checkpoint.clock;
            for(int i=0;i<bodies.Count;i++)
            {
                var b=bodies[i];var state=checkpoint.bodies[i];
                b.position=state.position;b.velocity=state.velocity;b.rotation=state.rotation;b.spin=state.spin;
                if(b.transform)b.transform.SetPositionAndRotation(new Vector3(b.position.x,b.position.y,0),Quaternion.Euler(0,0,b.rotation));
                if(b.renderer)b.renderer.sortingOrder=500+Mathf.RoundToInt((5-b.position.y)*10);
            }
        }
    }
}
