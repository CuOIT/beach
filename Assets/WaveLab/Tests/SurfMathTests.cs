using NUnit.Framework;
using UnityEngine;

namespace WaveLab.Tests
{
    public sealed class SurfMathTests
    {
        [Test] public void WetSand_DoesNotMarkTheReportedDryPoint()
        {
            Assert.AreEqual(0,SurfMath.WetSandMemory(new Vector2(1,-3.6f),2.45f));
        }
        [TestCase(.5f)] [TestCase(2.45f)] [TestCase(3.6f)]
        public void WetSand_RespectsMaximumReachAcrossTheCurvedShore(float reach)
        {
            foreach(float x in new[]{-4f,-2f,0,1,4})
            {
                float edge=SurfMath.Shore(x,.5f,reach);
                Assert.AreEqual(0,SurfMath.WetSandMemory(new Vector2(x,edge-.01f),reach));
                Assert.AreEqual(1,SurfMath.WetSandMemory(new Vector2(x,edge+.1f),reach));
            }
        }
        [Test] public void Shore_MaximumRunupOccursDuringPeakHold()
        {
            foreach(float x in new[]{-4f,0,4})
            {
                float peak=SurfMath.Shore(x,.5f,2.45f);
                for(int i=0;i<100;i++)Assert.GreaterOrEqual(SurfMath.Shore(x,i/100f,2.45f),peak);
            }
        }
    }
}
