using UnityEngine;
using UnityEngine.EventSystems;
namespace WaveLab
{
    public sealed class WaveLabPanelDrag : MonoBehaviour,IBeginDragHandler,IDragHandler
    {
        public RectTransform target,canvas;
        public void OnBeginDrag(PointerEventData e){}
        public void OnDrag(PointerEventData e)
        {
            if(!target||!canvas)return;
            float scale=GetComponentInParent<Canvas>().scaleFactor;
            target.anchoredPosition+=e.delta/scale;Clamp(target,canvas);
        }
        public static void Clamp(RectTransform target,RectTransform canvas)
        {
            var corners=new Vector3[4];target.GetWorldCorners(corners);
            Vector2 min=canvas.InverseTransformPoint(corners[0]),max=canvas.InverseTransformPoint(corners[2]);
            Rect bounds=canvas.rect;Vector2 move=Vector2.zero;
            if(min.x<bounds.xMin+8)move.x=bounds.xMin+8-min.x;
            if(max.x>bounds.xMax-8)move.x=bounds.xMax-8-max.x;
            if(min.y<bounds.yMin+8)move.y=bounds.yMin+8-min.y;
            if(max.y>bounds.yMax-8)move.y=bounds.yMax-8-max.y;
            target.anchoredPosition+=move;
        }
    }
}
