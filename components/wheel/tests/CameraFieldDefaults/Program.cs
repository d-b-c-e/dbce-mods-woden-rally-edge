using System.Text;
using System.Reflection;
using BepInEx.Configuration;
using WodenRallyEdge;
using WodenRallyEdge.Core;
if(args.Length!=2)throw new ArgumentException("Mode and fresh private root required");
string mode=args[0],root=Path.GetFullPath(args[1]);
if(Directory.Exists(root))throw new IOException("Preserve fixture evidence; fresh root required");
Directory.CreateDirectory(root);
int checks=0,index=0;string path="",beforeText="";CameraSlot selected=default;bool realistic=false;
CameraSlot[] cameras={new(MountedView.Bonnet),new(MountedView.Bumper),new(MountedView.Stock),new(MountedView.Stock,-1,true),new(MountedView.Stock,2)};
string[] fields={"Side","Height","Forward","Pitch","Fov"};
string Row(string field)=>field switch {"Side"=>"Side offset (m)","Height"=>"Height offset (m)","Forward"=>"Forward offset (m)","Pitch"=>"Tilt offset (degrees)",_=>"Current camera FOV (degrees)"};
string PoseKey(CameraSlot c,int car,bool set,string field)=>"Car"+car+"."+c.Key+(set?".Realistic.":".Game.")+field+"Offset";
string FovKey(CameraSlot c,bool set)=>c.Key+(set?"Realistic":"Game")+"OffsetDegrees";
string Value(string field)=>field switch {"Side"=>"1.25","Height"=>"2.5","Forward"=>"3.75",_=>"8"};
byte[] Bom(string text)=>new byte[]{0xef,0xbb,0xbf}.Concat(Encoding.UTF8.GetBytes(text)).ToArray();
void Check(bool ok,string label){checks++;if(!ok)throw new InvalidOperationException("ASSERT "+label);}
void Near(double actual,double expected,string label)=>Check(Math.Abs(actual-expected)<1e-5,label+" actual="+actual+" expected="+expected);
float Axis(CameraOffset value,string field)=>field switch {"Side"=>value.Side,"Height"=>value.Height,"Forward"=>value.Forward,_=>value.Pitch};
void Setup(CameraSlot camera,bool set,bool omitPose=false,bool omitFov=false){
 selected=camera;realistic=set;path=Path.Combine(root,"case-"+(++index)+".cfg");
 var s=new StringBuilder("# owner text\r\n[Triple]\n");
 foreach(var c in cameras){
  s.Append(c.Key+"Realistic = "+(c.Key==camera.Key?set:!set).ToString().ToLowerInvariant()+"\r\n");
  foreach(bool choice in new[]{false,true}){
   if(!(omitFov&&c.Key==camera.Key&&choice==set))s.Append(FovKey(c,choice)+" \t= 17 \t\n");
   foreach(int car in new[]{1,2})foreach(string field in fields.Take(4))
    if(!(omitPose&&car==1&&c.Key==camera.Key&&choice==set))s.Append(PoseKey(c,car,choice,field)+" \t= "+Value(field)+" \t\r\n");
  }
 }
 s.Append("[Future]\r\nKeep = exact\n");beforeText=s.ToString();File.WriteAllBytes(path,Bom(beforeText));
 TripleView.Open(path,camera);TripleView.CurrentCar=1;Panel.Frame();
}
void Draw(string field){Panel.Frame(Row(field));Panel.Draw();}
void Direct(string method,params object[] args)=>typeof(TripleView).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
void CurrentDefault(string field,bool named=false){
 Draw(field);
 if(field=="Fov"){
  Near(TripleView.Store.FovOffset(selected,realistic),0,named?"DEFAULT_FOV":"current FOV offset zero");
  Near(TripleView.Store.Position(selected,1,realistic).Side,1.25,"FOV Default preserves position");
  Check(TripleView.ViewsInvalidated,"FOV default invalidates projection cache");
  TripleView.TryGetPanelFov(out float lens);Near(lens,realistic?TripleView.MeasuredFov:70,"FOV default returns calculated start");
 }else{
  var pose=TripleView.Store.Position(selected,1,realistic);
  Near(Axis(pose,field),0,named?"DEFAULT_SIDE":"current pose field zero "+field);
  foreach(string other in fields.Take(4).Where(f=>f!=field))Near(Axis(pose,other),float.Parse(Value(other),System.Globalization.CultureInfo.InvariantCulture),"other pose field retained "+other);
  Near(TripleView.Store.FovOffset(selected,realistic),17,"position Default preserves shared FOV");
 }
 string key=field=="Fov"?FovKey(selected,realistic):PoseKey(selected,1,realistic,field);
 string original=field=="Fov"?"17":Value(field);
 string after=beforeText.Replace(key+" \t= "+original+" \t",key+" \t= 0 \t");
 Check(File.ReadAllBytes(path).SequenceEqual(Bom(after)),"one owned scalar only; BOM/mixed endings/format/all other namespaces exact");
 Check(Panel.Clicked&&Panel.Defaults.Count==5,"all five actual Default controls present and selected one invoked");
 Check(Panel.Buttons.Where(b=>b.Caption=="Default").All(b=>b.Bounds.width==96&&b.Bounds.x+b.Bounds.width<=944.001f),"actual row geometry reserves Default width");
 Check(Panel.DirtyCalls==0,"camera defaults never use whole-settings Dirty/save");
 Near(TripleView.Store.Position(selected,2,realistic).Side,1.25,"other car position retained");
 Near(TripleView.Store.FovOffset(selected,!realistic),17,"other set shared FOV retained");
}
void Failures(){
 Setup(cameras[0],false);Panel.Draw();var before=File.ReadAllBytes(path);
 using(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)){Draw("Side");}
 Check(File.ReadAllBytes(path).SequenceEqual(before),"locked position default preserves last-good bytes");
 Near(TripleView.Store.Position(selected,1,false).Side,1.25,"locked default does not publish failed cache value");
 Check(TripleView.Notice.Contains("Couldn't restore position default"),"actual position handler reports failure");
 Draw("Side");Near(TripleView.Store.Position(selected,1,false).Side,0,"explicit position retry succeeds");
 Setup(cameras[0],false);Panel.Draw();before=File.ReadAllBytes(path);
 using(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)){Draw("Fov");}
 Check(File.ReadAllBytes(path).SequenceEqual(before),"locked FOV default preserves bytes");
 Near(TripleView.Store.FovOffset(selected,false),17,"locked FOV default retains cache");
 Check(TripleView.Notice.Contains("Couldn't restore FOV default"),"actual FOV handler reports failure");
 Draw("Fov");Near(TripleView.Store.FovOffset(selected,false),0,"explicit FOV retry succeeds");
 Setup(cameras[0],false);Panel.Draw();File.AppendAllText(path,"\n[Triple]\n"+PoseKey(selected,1,false,"Side")+" = 2.5\n");before=File.ReadAllBytes(path);Draw("Side");
 Check(File.ReadAllBytes(path).SequenceEqual(before),"duplicate owned position key refuses without write");
 Check(TripleView.Notice.Contains("Couldn't restore"),"duplicate refusal reaches actual notice");
 Setup(cameras[0],false);before=File.ReadAllBytes(path);TripleView.CameraEditAllowed=false;Draw("Side");
 Check(!Panel.Clicked&&Panel.Defaults.Count==0&&File.ReadAllBytes(path).SequenceEqual(before),"unavailable editing hides all defaults without write");
 TripleView.CameraEditAllowed=true;TripleView.SourceAvailable(false);Draw("Fov");
 Check(!Panel.Clicked&&File.ReadAllBytes(path).SequenceEqual(before),"missing source hides FOV default");
 Check(Directory.GetFiles(root,"*.tmp").Length==0,"actual writer removes temporary files");
}
void SavedPoseAndReset(){
 Setup(cameras[0],false,omitPose:true);MountedCamera.Saved=new CameraOffset(9,8,7,6);
 Direct("DefaultPositionFromPanel","Side");
 var pose=TripleView.Store.Position(selected,1,false,MountedCamera.Saved);
 Near(pose.Side,0,"saved-pose fallback resets without requiring prior UI/cache priming");
 Near(pose.Height,8,"other saved-pose fallback preserved");
 var text=File.ReadAllText(path);string prefix="Car1."+selected.Key+".Game.";
 Check(text.Contains(prefix+"SideOffset")&&!text.Contains(prefix+"HeightOffset")&&!text.Contains(prefix+"ForwardOffset")&&!text.Contains(prefix+"PitchOffset"),"saved-pose position publishes exactly one new key");
 TripleView.Open(path,selected);MountedCamera.Saved=new CameraOffset(9,8,7,6);
 Near(TripleView.Store.Position(selected,1,false,MountedCamera.Saved).Side,0,"saved-pose default survives reload");
 Setup(cameras[0],false,omitFov:true);Runtime.Settings.CameraFov=83;Direct("DefaultFovFromPanel");
 TripleView.Open(path,selected);Runtime.Settings.CameraFov=83;
 Near(TripleView.Store.FovOffset(selected,false,13),0,"explicit FOV default overrides the saved-pose offset after reload without cache priming");
 Runtime.Settings.CameraFov=70;
 Setup(cameras[0],false);Panel.Frame("*","Reset current camera/set (Numpad 5)");Panel.Draw();
 Check(Panel.Clicked&&TripleView.Store.Position(selected,1,false)==default&&TripleView.Store.FovOffset(selected,false)==0,"actual whole Reset still clears pose and FOV");
 Near(TripleView.Store.Position(selected,2,false).Side,1.25,"whole Reset preserves other car");
 Near(TripleView.Store.FovOffset(selected,true),17,"whole Reset preserves other set");
 Setup(cameras[0],false);Panel.Draw();File.AppendAllText(path,"OwnerLater = retained\n");Draw("Height");
 Check(File.ReadAllText(path).Contains("OwnerLater = retained\n"),"later unrelated owner edit retained by real writer");
 var before=File.ReadAllBytes(path);Draw("Height");Check(File.ReadAllBytes(path).SequenceEqual(before),"explicit repeated default is byte-idempotent");
 Near(Panel.Plain(.3f),.3,"ordinary sliders keep their existing behavior");
 Check(Panel.Defaults.Count==0,"ordinary rows do not gain camera commands");
}

double Current(string field) => field=="Fov"
 ? (realistic?TripleView.MeasuredFov:70)+TripleView.Store.FovOffset(selected,realistic)
 : Axis(TripleView.Store.Position(selected,1,realistic),field);
void Seed(string field,float value) {
 if(field=="Fov")TripleView.Store.SetFov(selected,realistic,realistic?TripleView.MeasuredFov:70,value);
 else {var pose=TripleView.Store.Position(selected,1,realistic);pose=field switch {
 "Side"=>pose with{Side=value},"Height"=>pose with{Height=value},"Forward"=>pose with{Forward=value},_=>pose with{Pitch=value}};
 TripleView.Store.SetPosition(selected,1,realistic,pose);}
}
void Input(string field,string action) {
 var row=Row(field);Panel.Frame(row,action is "+" or "\u2212"?action:"__none");
 if(action is "Right" or "Left" or "Menu") {
  Panel.EventRow=row;
  if(action=="Menu")typeof(Panel).GetField("_menuStep",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,1);
  else UnityEngine.Event.current=new(){type=UnityEngine.EventType.KeyDown,keyCode=action=="Right"?UnityEngine.KeyCode.RightArrow:UnityEngine.KeyCode.LeftArrow};
 }
 if(action=="Drag") {
  Panel.Frame();Panel.Draw();var track=Panel.Tracks[row];Panel.Frame(row,"__none");
  UnityEngine.Event.current=new(){type=UnityEngine.EventType.MouseDown,button=0,mousePosition=new(){x=track.x+track.width*.999f,y=track.y+14}};
 }
 Panel.Draw();
}
void NamedRange(string field) {
 Setup(cameras[0],false);Seed(field,field=="Fov"?500:10);double before=Current(field);
 Input(field,"+");
 Check(Current(field)>before,field=="Fov"?"RANGE_FOV_REQUEST_RETAINED":"RANGE_SIDE_NO_JUMP");
}
void Ranges() {
 foreach(var camera in cameras)foreach(bool set in new[]{false,true})foreach(string field in fields)
 foreach(float value in field=="Fov"?new[]{-300f,0,20,34,70,120,179,500,1e8f}:new[]{-1000f,-10,0,10,1000,1e8f}) {
  Setup(camera,set);Seed(field,value);var before=File.ReadAllBytes(path);Panel.Frame();Panel.Draw();
  Check(File.ReadAllBytes(path).SequenceEqual(before),"passive outside-range draw writes nothing "+field);
  double start=Current(field);var pose=TripleView.Store.Position(selected,1,set);var otherFov=TripleView.Store.FovOffset(selected,!set);
  Input(field,"+");double plus=Current(field);
  Check(plus>start&&plus-start<Math.Max(5,Math.Abs(start)*.05),"actual plus moves outward without endpoint jump "+field+" start="+start+" plus="+plus);
  Input(field,"\u2212");Check(Current(field)<plus,"actual minus moves back "+field+" start="+start+" plus="+plus+" now="+Current(field));
  TripleView.Open(path,camera);Check(Current(field)!=0||value==0||field=="Fov","outside-range value survives reload "+field);
  Near(TripleView.Store.FovOffset(selected,!set),otherFov,"other set untouched");
  if(field=="Fov")Check(TripleView.Store.Position(selected,1,set)==pose,"FOV edits preserve all pose axes");
  else foreach(var other in fields.Take(4).Where(f=>f!=field))
   Near(Axis(TripleView.Store.Position(selected,1,set),other),Axis(pose,other),"range edit preserves other axis");
 }
 foreach(string field in fields)foreach(string action in new[]{"Right","Left","Menu","Drag"}) {
  Setup(cameras[0],false);Seed(field,field=="Fov"?500:1000);double start=Current(field);
  Input(field,action);double after=Current(field);
  Check(action=="Left"?after<start:after>start,"actual keyboard/menu/drag path moves at outside value "+field+"/"+action);
  Check(Math.Abs(after-start)<Math.Abs(start)*.1,"input path cannot jump back to old endpoint "+field+"/"+action);
 }
 foreach(string field in fields)foreach(float value in new[]{float.MaxValue,-float.MaxValue}) {
  Setup(cameras[0],false);Seed(field,value);Panel.Frame();Panel.Draw();var before=File.ReadAllBytes(path);
  Check(File.ReadAllBytes(path).SequenceEqual(before),"extreme finite passive editor does not save");
  Input(field,value>0?"\u2212":"+");
  Check(double.IsFinite(Current(field)),"extreme editor nudge stays finite "+field);
 }
 Setup(cameras[0],false);Seed("Side",1000);Panel.Frame();Panel.Draw();var locked=File.ReadAllBytes(path);
 using(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)){Input("Side","+");}
 Check(File.ReadAllBytes(path).SequenceEqual(locked)&&Current("Side")==1000,"locked outside-range edit keeps cache and bytes");
 Check(TripleView.Notice.Contains("Couldn't save position"),"actual outside-range failure notice");
 Input("Side","+");Check(Current("Side")>1000,"explicit retry after failed outside-range edit");
 Setup(cameras[0],false);Seed("Fov",500);Panel.Frame();Panel.Draw();
 Check(TripleView.TryGetPanelFov(out float lens)&&lens==179,"effective lens still limited while request editor is unbounded");
 Check(Current("Fov")==500,"showing saturated FOV does not replace saved request");
 Check(Panel.DirtyCalls==0,"range editors do not trigger whole settings save");
 Near(Panel.Plain(.3f),.3,"non-camera slider behavior retained");
}

try{
 if(mode=="pose"){Setup(cameras[0],false);CurrentDefault("Side",true);}
 else if(mode=="range-pose"){NamedRange("Side");}
 else if(mode=="range-fov"){NamedRange("Fov");}
 else if(mode=="ranges"){Ranges();}
 else if(mode=="fov"){Setup(cameras[0],false);CurrentDefault("Fov",true);}
 else if(mode=="all"){
  foreach(var camera in cameras)foreach(bool set in new[]{false,true})foreach(string field in fields){Setup(camera,set);CurrentDefault(field);}
  Failures();SavedPoseAndReset();
 }else throw new ArgumentException("Unknown mode");
 Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{pass=true,checks,actualSlider=true,actualConsumers=true,realStore=true,engineSimulated=true}));return 0;
}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
