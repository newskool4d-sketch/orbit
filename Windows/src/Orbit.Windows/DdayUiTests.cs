using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace Orbit;

internal static class DdayUiTests
{
    // Called only by --ui-smoke, whose DdayStore is isolated in memory.
    public static async Task<bool> Run(CoreWebView2 core, string? captureDirectory,Func<Task> simulateResume)
    {
        await core.ExecuteScriptAsync("""
            window.__orbitDdayQaDone=false;window.__orbitDdayQaPass=false;
            void(async()=>{
              const require=value=>{if(!value)throw new Error('fixture-check')};
              try{
                require(state.capabilities.dday===true&&!state.settings.googleConnected);
                const count=state.ddays.length;
                openDdayManager();
                document.querySelector('#dday-title').value='통합 검증용 중요한 날짜와 긴 한글 제목의 줄바꿈 확인';
                document.querySelector('#dday-date').value='2030-12-31';
                await submitDday({preventDefault(){}});
                require(state.ddays.length===count+1);
                const entry=state.ddays[state.ddays.length-1],id=entry.id;
                openDdayManager(id);
                document.querySelector('#dday-date').value='2031-01-01';
                await submitDday({preventDefault(){}});
                require(state.ddays.find(x=>x.id===id).targetDate==='2031-01-01');
                await send('updateDday',{id,title:entry.title,targetDate:'2031-01-01',pinned:true});
                require(state.ddays.find(x=>x.id===id).pinned);
                await send('archiveDday',{id,value:true});
                require(state.ddays.find(x=>x.id===id).archived);
                require(!document.querySelector('#dday-section [data-dday-edit="'+id+'"]'));
                await send('archiveDday',{id,value:false});
                require(!state.ddays.find(x=>x.id===id).archived);
                document.querySelector('[data-dday-delete="'+id+'"]').click();
                require(!document.querySelector('[data-dday-confirm="'+id+'"]').hidden);
                require(state.ddays.length===count+1);
                await handleDdayAction({dataset:{ddayAction:'delete',ddayId:id}});
                require(state.ddays.length===count);
                closeDdayManager();
                require(!document.querySelector('#content').inert);
                window.__orbitDdayQaPass=true;
              }catch{window.__orbitDdayQaPass=false}
              finally{window.__orbitDdayQaDone=true}
            })();
            """);
        bool done=false;
        for(int attempt=0;attempt<100&&!done;attempt++)
        {
            await Task.Delay(50);
            done=await core.ExecuteScriptAsync("window.__orbitDdayQaDone")=="true";
        }
        bool crud=done&&await core.ExecuteScriptAsync("window.__orbitDdayQaPass")=="true";
        var keyboard=await core.ExecuteScriptAsync("""
            (()=>{
              const key=(target,key,extra={})=>target.dispatchEvent(new KeyboardEvent('keydown',{key,bubbles:true,cancelable:true,...extra}));
              document.querySelector('#search-shortcut').focus();
              openDdayManager();
              const dialog=document.querySelector('#dday-manager');
              const focusables=Array.from(dialog.querySelectorAll('button:not(:disabled),input:not(:disabled):not([type="hidden"])')).filter(node=>!node.closest('[hidden]'));
              focusables[0].focus();key(focusables[0],'Tab',{shiftKey:true});
              const backward=document.activeElement===focusables[focusables.length-1];
              key(document.activeElement,'Tab');
              const forward=document.activeElement===focusables[0];
              key(document.activeElement,'Escape');
              const escaped=dialog.hidden&&document.activeElement.id==='search-shortcut';
              key(document,'k',{ctrlKey:true});
              const shortcut=document.activeElement.id==='file-search'&&!document.querySelector('#files').hidden;
              return backward&&forward&&escaped&&shortcut;
            })()
            """);
        bool keyboardPass=keyboard=="true";
        await core.ExecuteScriptAsync("window.__orbitResumeCalls=0;window.__orbitOriginalRefreshDdayClock=window.refreshDdayClock;window.refreshDdayClock=()=>{window.__orbitResumeCalls++;return window.__orbitOriginalRefreshDdayClock()};");
        await simulateResume();
        bool resumePass=await core.ExecuteScriptAsync("window.__orbitResumeCalls===1")=="true";
        bool layout=true;
        foreach(var theme in new[]{"moss","pearl","cobalt"})
        foreach(var size in new[]{"normal","large","xlarge"})
        {
            await core.ExecuteScriptAsync("state.theme='"+theme+"';state.settings.textSize='"+size+"';render();openDdayManager(state.ddays[0].id);");
            await Task.Delay(80);
            var fits=await core.ExecuteScriptAsync("(()=>{const m=document.querySelector('#dday-manager'),s=m.querySelector('.settings-scroll');return m.scrollWidth<=m.clientWidth+1&&s.scrollWidth<=s.clientWidth+1&&document.activeElement.id==='dday-title'})()");
            layout&=fits=="true";
            if(!string.IsNullOrEmpty(captureDirectory))
            {
                Directory.CreateDirectory(captureDirectory);
                using var shot=File.Create(Path.Combine(captureDirectory,"windows-dday-"+theme+"-"+size+".png"));
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
            }
            await core.ExecuteScriptAsync("closeDdayManager();");
        }
        Console.WriteLine(JsonSerializer.Serialize(new{test="dday-native-ui",pass=crud&&layout&&keyboardPass&&resumePass,crud,layout,keyboard=keyboardPass,resume=resumePass,combinations=9}));
        return crud&&layout&&keyboardPass&&resumePass;
    }
}
