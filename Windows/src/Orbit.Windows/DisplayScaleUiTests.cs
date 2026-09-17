using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Orbit;

internal static class DisplayScaleUiTests
{
    public static async Task<bool> Run(WebView2 web,string? captureDirectory)
    {
        var results=new List<object>();
        bool pass=true;
        var window=Window.GetWindow(web);
        var originalWidth=window.Width;
        var originalHeight=window.Height;
        var workArea=SystemParameters.WorkArea;
        foreach(var percent in new[]{100,125,150,200})
        {
            var factor=percent/100d;
            var logicalWidth=Math.Min(560,Math.Floor(workArea.Width/factor));
            var logicalHeight=Math.Min(700,Math.Floor(workArea.Height/factor));
            window.Width=logicalWidth*factor;
            window.Height=logicalHeight*factor;
            web.ZoomFactor=factor;
            await Task.Delay(150);
            foreach(var textSize in new[]{"normal","xlarge"})
            {
                await web.CoreWebView2.ExecuteScriptAsync($"state.settings.textSize='{textSize}';render();");
                foreach(var surface in new[]{"today","files","agents","settings","dday"})
                {
                    await Show(web.CoreWebView2,surface);
                    var json=await web.CoreWebView2.ExecuteScriptAsync("""
                        JSON.stringify((()=>{
                          const visible=e=>{const s=getComputedStyle(e),r=e.getBoundingClientRect();return s.display!=='none'&&s.visibility!=='hidden'&&r.width>0&&r.height>0};
                          const selectors=['#app','header','.greeting','nav','#content','footer','#settings .settings-heading','#settings .settings-scroll','#dday-manager .settings-heading','#dday-manager .settings-scroll','.search','.filters','.themes','.text-sizes','.path-row','.connection-row','.settings-bottom','#dday-form','.dday-main','.dday-actions','.dday-delete-confirm'];
                          const violations=[];
                          for(const selector of selectors)for(const e of document.querySelectorAll(selector))if(visible(e)){
                            const r=e.getBoundingClientRect();
                            if(e.scrollWidth>e.clientWidth+2)violations.push(selector+':overflow');
                            if(r.left<-2||r.right>innerWidth+2)violations.push(selector+':edge');
                          }
                          for(const selector of ['#app','header','.greeting','nav','footer','#settings','#dday-manager','.settings-heading'])for(const e of document.querySelectorAll(selector))if(visible(e)){
                            const r=e.getBoundingClientRect();
                            if(r.top<-2||r.bottom>innerHeight+2)violations.push(selector+':vertical-edge');
                            if((selector==='#app'||selector==='footer'||selector==='header'||selector==='nav')&&e.scrollHeight>e.clientHeight+2)violations.push(selector+':vertical-overflow');
                          }
                          for(const selector of ['header','nav','footer','.settings-heading','.path-row','.connection-row','.settings-bottom','.dday-actions'])for(const parent of document.querySelectorAll(selector))if(visible(parent)){
                            const children=Array.from(parent.children).filter(visible);
                            for(let i=0;i<children.length;i++)for(let j=i+1;j<children.length;j++){
                              const a=children[i].getBoundingClientRect(),b=children[j].getBoundingClientRect();
                              if(Math.min(a.right,b.right)-Math.max(a.left,b.left)>2&&Math.min(a.bottom,b.bottom)-Math.max(a.top,b.top)>2)violations.push(selector+':overlap');
                            }
                          }
                          return {pass:violations.length===0,width:innerWidth,height:innerHeight,violations:[...new Set(violations)]};
                        })())
                        """);
                    var decoded=JsonSerializer.Deserialize<string>(json)??"{}";
                    var result=JsonSerializer.Deserialize<JsonElement>(decoded);
                    bool surfacePass=result.GetProperty("pass").GetBoolean();
                    pass&=surfacePass;
                    results.Add(new{percent,textSize,surface,pass=surfacePass,width=result.GetProperty("width").GetInt32(),height=result.GetProperty("height").GetInt32(),violations=result.GetProperty("violations")});
                    if(!string.IsNullOrEmpty(captureDirectory)&&textSize=="xlarge"&&(surface=="today"||surface=="dday"))
                    {
                        Directory.CreateDirectory(captureDirectory);
                        using var shot=File.Create(Path.Combine(captureDirectory,$"windows-scale-{percent}-{surface}.png"));
                        await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
                    }
                }
            }
        }
        web.ZoomFactor=1;
        window.Width=originalWidth;
        window.Height=originalHeight;
        await web.CoreWebView2.ExecuteScriptAsync("state.settings.textSize='normal';render();closeDdayManager();settings(false);tab('today');");
        Console.WriteLine(JsonSerializer.Serialize(new{test="windows-display-scale",pass,combinations=40,results}));
        return pass;
    }

    static async Task Show(CoreWebView2 core,string surface)
    {
        var script=surface switch {
            "settings"=>"closeDdayManager();settings();",
            "dday"=>"settings(false);tab('today');openDdayManager(state.ddays[0]?.id);",
            _=>$"closeDdayManager();settings(false);tab('{surface}');"
        };
        await core.ExecuteScriptAsync(script);
        await Task.Delay(60);
    }
}
