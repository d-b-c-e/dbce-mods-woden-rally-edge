using WodenRallyEdge.Core;
using UnityEngine;
namespace UnityEngine {
 internal struct Rect(float a,float b,float c,float d) {internal float x=a,y=b,width=c,height=d;}
 internal struct Vector2 {internal float x,y;}
 internal struct Color(float r,float g,float b) {internal static Color white=>new(1,1,1);}
 internal enum EventType {Repaint,MouseDown,MouseDrag,MouseUp,Used,KeyDown}
 internal enum KeyCode {None,LeftArrow,RightArrow}
 internal sealed class Event {internal static Event current=new();internal EventType type=EventType.Repaint;internal int button;internal Vector2 mousePosition;internal KeyCode keyCode;internal void Use()=>type=EventType.Used;}
 internal static class GUIUtility {internal static int keyboardControl;}
}
namespace WodenRallyEdge {
 internal static partial class Panel {
  private static int _drag=-1,_focus=-1,_ordinal,_menuStep;private static float _scale=1;
  private static Color Accent=new(1,1,1);
  internal static string ClickRow="",ClickCaption="Default",CurrentRow="";
  internal static bool Clicked;internal static int DirtyCalls;
  internal static readonly HashSet<string> Defaults=new();
  internal static readonly List<(string Row,string Caption,Rect Bounds)> Buttons=new();
  internal static void Frame(string click="",string caption="Default"){
   ClickRow=click;ClickCaption=caption;Clicked=false;Defaults.Clear();Buttons.Clear();_ordinal=0;_drag=-1;_focus=-1;_menuStep=0;GUIUtility.keyboardControl=0;Event.current=new();
  }
  private static bool Button(float x,float y,float width,string text,bool selected=false,bool enabled=true){
   Buttons.Add((CurrentRow,text,new Rect(x,y,width,36)));
   if(text=="Default")Defaults.Add(CurrentRow);
   if(!Clicked&&enabled&&text==ClickCaption&&(ClickRow=="*"||ClickRow==CurrentRow)){Clicked=true;Event.current.Use();return true;}return false;
  }
  private static void Label(float x,float y,float width,string text){if(x==250)CurrentRow=text;}
  private static void Dirty()=>DirtyCalls++;
  private static Rect R(float x,float y,float width,float height)=>new(x,y,width,height);
  private static bool Visible(Rect r)=>true;
  private static bool Inside(Rect r,Vector2 position)=>false;
  private static void Reveal(Rect r){}
  private static Color ColorOf(object color)=>new(1,1,1);
  private static void Fill(Rect r,Color color){}
  internal static void Draw(){DrawFov(152);DrawPositions(300);}
  internal static float Plain(float value){Frame();Slider(10,"Plain",ref value,0,1,save:false);return value;}
 }
}
