using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace Orbit;

internal static class PersistenceUiTests
{
    public static async Task<bool> Run(CoreWebView2 core,string phase)
    {
        var script=phase=="seed" ? """
            window.__orbitPersistenceDone=false;window.__orbitPersistencePass=false;
            void(async()=>{try{
              await send('textSize',{value:'xlarge'});
              openDdayManager();
              document.querySelector('#dday-title').value='재시작 복원 검증일';
              document.querySelector('#dday-date').value='2032-03-01';
              await submitDday({preventDefault(){}});
              window.__orbitPersistencePass=state.settings.textSize==='xlarge'&&state.ddays.length===1&&document.body.dataset.textSize==='xlarge';
            }catch{window.__orbitPersistencePass=false}finally{window.__orbitPersistenceDone=true}})();
            """ : """
            window.__orbitPersistenceDone=false;window.__orbitPersistencePass=false;
            void(async()=>{try{
              const entry=state.ddays[0];
              window.__orbitPersistencePass=state.settings.textSize==='xlarge'&&state.ddays.length===1&&entry.targetDate==='2032-03-01'&&document.body.dataset.textSize==='xlarge'&&!!document.querySelector('[data-dday-edit="'+entry.id+'"]');
            }catch{window.__orbitPersistencePass=false}finally{window.__orbitPersistenceDone=true}})();
            """;
        await core.ExecuteScriptAsync(script);
        bool done=false;
        for(int attempt=0;attempt<100&&!done;attempt++){await Task.Delay(50);done=await core.ExecuteScriptAsync("window.__orbitPersistenceDone")=="true";}
        bool pass=done&&await core.ExecuteScriptAsync("window.__orbitPersistencePass")=="true";
        var stateJson=await core.ExecuteScriptAsync("JSON.stringify({textSize:state.settings.textSize,ddays:state.ddays.length})");
        var decoded=JsonSerializer.Deserialize<string>(stateJson)??"{}";
        var state=JsonSerializer.Deserialize<JsonElement>(decoded);
        Console.WriteLine(JsonSerializer.Serialize(new{test="restart-persistence-"+phase,pass,textSize=state.GetProperty("textSize").GetString(),ddays=state.GetProperty("ddays").GetInt32()}));
        return pass;
    }
}
