// Device-free cross-language fixture. Compiled against the exact native source
// pin, then its real Core frames feed Woden's production query/dispatch tests.
#include "shim.hpp"
#include "shim_fake_provider.hpp"
#include "dbce_shim.h"
#include <fstream>
#include <iostream>
#include <map>
#include <stdexcept>
using namespace dbce::shim;
int main(int argc,char** argv)try{
    if(argc!=3)throw std::runtime_error("profile and fresh frame output required");
    std::ifstream input(argv[1],std::ios::binary);
    std::string bytes((std::istreambuf_iterator<char>(input)),{});
    auto profile=read_profile(bytes);
    if(!profile.ok||!profile.section.error.empty())throw std::runtime_error("profile refused: "+profile.error+profile.section.error);
    fake::Provider provider;std::map<std::string,std::size_t> devices;
    for(const auto& entry:profile.section.bound){
        const auto& b=entry.binding;
        if(devices.count(b.dev))continue;
        fake::Device d;d.listed={b.dev,b.prod,"device-free fixture"};d.caps=fake::wheel_caps();
        for(int i=0;i<8;++i){d.caps.axis[i]=d.caps.range_ok[i]=true;d.caps.min[i]=0;d.caps.max[i]=65535;}
        d.caps.pov.fill(true);d.state.buttons[31]=0x80; // Physical Confirm must never leak into test-neutral.
        devices[b.dev]=provider.add(d);
    }
    CoreConfig config;config.latched=true;
    for(int i=0;i<ActionCount;++i){std::string id=action_name(i);if(id!="confirm"&&id!="start"&&id!="navLeft"&&id!="navRight")config.capability.unsupported[i]="outside bounded Woden menu trial";}
    Core core(provider,config);core.set_profile(profile);
    const std::string nonce(32,'a');core.request_admission(Session{nonce,2000000100},2000000000,true,true);
    std::ofstream output(argv[2],std::ios::binary|std::ios::trunc);
    if(!output)throw std::runtime_error("output unavailable");
    const char* actions[]={"confirm","start","navRight","navLeft"};
    int injected=0,neutral=0;
    for(int phase=0;phase<5;++phase)for(int tick=0;tick<100;++tick){
        if(tick==35){
            std::string command=phase==4?"end":"inject action "+std::string(actions[phase])+" 1 ms=150";
            auto result=core.offer_batch("nonce="+nonce+"\nbatch="+std::to_string(phase+1)+"\n"+command+"\n");
            if(result.verdict==BatchVerdict::Refused)throw std::runtime_error("Core batch refused: "+result.why);
        }
        core.service_devices();provider.advance(10000);const auto& f=core.tick();
        if(f.run==Run::Failed||f.run==Run::Expired||f.run==Run::Inert)throw std::runtime_error("Core run failed");
        dbce_shim_frame wire{};wire.struct_size=sizeof wire;wire.abi=2;wire.seq=f.seq;wire.tick_us=f.tick_us;
        wire.down=f.down;wire.up=f.up;wire.run=std::uint8_t(f.run);wire.latched=f.latched;wire.suppress_native=f.suppress_native;wire.sample_count=std::uint32_t(f.samples.size());
        for(int i=0;i<ActionCount;++i){const auto& a=f.action[i];wire.action[i].value=a.value;wire.action[i].status=std::uint8_t(a.status);wire.action[i].injected=a.injected;
            if(a.injected)++injected;if(a.status==Status::TestNeutral){++neutral;if(a.value!=0)throw std::runtime_error("physical input escaped neutral");}}
        static_assert(sizeof wire==296);output.write(reinterpret_cast<const char*>(&phase),4);output.write(reinterpret_cast<const char*>(&wire),sizeof wire);
    }
    if(core.control_state().run!=Run::Completed||injected==0||neutral==0||provider.misuse)throw std::runtime_error("incomplete Core evidence");
    std::cout<<"PASS actual pinned Core: 500 frames, "<<injected<<" injected actions, "<<neutral<<" neutral actions; fake devices only\n";
    return 0;
}catch(const std::exception& e){std::cerr<<e.what()<<'\n';return 1;}
