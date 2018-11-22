﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;


namespace HotloadPong
{
   
    public class Hotloader 
    {
        dynamic logic;
        dynamic state;
        // For gamelogic hotloading
        Assembly asm; // The current loaded gamelogic assembly
        DateTime lastUpdateDLL; // Last time the gamelogic dll file was updated        
        string solutionPath;
        string executionPath;

        //For Shader Hotloading
#if DEBUG
        ContentManager shaderContent;
        DateTime lastUpdateShaders;
        //Location of mgcb executable, may be different on your system.
        string mgcbPathExe = @"C:\Program Files (x86)\MSBuild\MonoGame\v3.0\Tools\MGCB.exe";
#endif
        
        ContentManager content;
        GraphicsDevice device;


        public Hotloader(ContentManager content, GraphicsDevice device)
        {
            //This gets the execution directory, 5 folders deep from solution
            //Adjust as necessary for your project structure
            executionPath = AppDomain.CurrentDomain.BaseDirectory;
            solutionPath = executionPath + @"..\..\..\..\..";

            //Load gamelogic dll
            LoadDLL();            
            //Setup shader hotloading
            this.content = content;
            this.device = device;            
#if DEBUG
            shaderContent = new ContentManager(content.ServiceProvider, content.RootDirectory);
            lastUpdateShaders = DateTime.Now;
#endif
            
        }

        public void LoadDLL()
        {
            var path = solutionPath + @"\GameLogic\bin\Debug\GameLogic.dll";
            lastUpdateDLL = File.GetLastWriteTime(path);
            asm = Assembly.Load(File.ReadAllBytes(path));

            // Find out gamelogic class in the loaded dll
            foreach (Type type in asm.GetExportedTypes())
            {
                if (type.FullName == "GameLogic.GameLogic")
                {
                    // We found our gamelogic type, set our dynamic types logic, and state
                    logic = Activator.CreateInstance(type);
                    // Don't set state if it already exists, we are going to keep that state
                    if (state == null)
                    {
                        state = logic.GetState();
                    }
                    break;
                }
                                
            }                     
    }

       

        public void Update(KeyboardState keyboard, GameTime gameTime)
        {
            logic.Update(keyboard, gameTime);
        }

        public void Draw(SpriteBatch batch, GameTime gameTime)
        {            
            logic.Draw(batch, gameTime);
        }

        public dynamic GetState()
        {
            return state;
        }

        // Called once up loading up a new instance of the DLL to set the game state
        // where it was.
        public void SetState()
        {
            // Because the GameState won't be the *same* gamestate as before
            // We have to copy or serialize the old game state object into the new one
            // You could use binary serializer or something here except
            // Monogame's types aren't tagged as serializeable
            // So come up with whatever scheme you want.

            //Get the updated state object, initailized to initial state
            dynamic newState = logic.GetState();  
            //Set everything to the state it was when the reload happened
            newState.device = state.device;
            newState.playerTex = state.playerTex;
            newState.shaders = state.shaders;
            newState.PlayerPos = state.PlayerPos;
            newState.jumpStart = state.jumpStart;
            state = newState;
        }

        // Check to see if the DLL has a newer date than when we last loaded it
        public void CheckDLL()
        {
            var path = solutionPath + @"\GameLogic\bin\Debug\GameLogic.dll";
            var update = File.GetLastWriteTime(path);
            if (update > lastUpdateDLL)
            {
                asm = null;
                //Load the new DLL and then set the state 
                LoadDLL();
                SetState();
            }
        }



#if DEBUG
        public void CheckShaders()
        {

            var files = Directory.GetFiles(solutionPath + @"/PlatformLayer/Content", "*.fx");
            foreach (var file in files)
            {
                var t = File.GetLastWriteTime(file);
                if (t > lastUpdateShaders)
                {
                    ShaderChanged(file);
                    lastUpdateShaders = t;
                }
            }

        }

        public void ShaderChanged(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            Process pProcess = new Process
            {
                StartInfo =
                       {
                            FileName = mgcbPathExe,
                            Arguments = "/platform:Windows /config: /profile:Reach /compress:False /importer:EffectImporter /processor:EffectProcessor /processorParam:DebugMode=Auto /build:"+name+".fx",
                            CreateNoWindow = true,
                            WorkingDirectory = solutionPath+@"\PlatformLayer\Content",
                            UseShellExecute = false,
                            RedirectStandardError = true,
                            RedirectStandardOutput = true
                        }
            };

            //Get program output
            string stdError = null;
            StringBuilder stdOutput = new StringBuilder();
            pProcess.OutputDataReceived += (sender, args) => stdOutput.Append(args.Data);


            pProcess.Start();
            pProcess.BeginOutputReadLine();
            stdError = pProcess.StandardError.ReadToEnd();
            pProcess.WaitForExit();

            string builtPath = solutionPath + @"\PlatformLayer\Content\" + name + ".xnb";
            string movePath = executionPath + "/Content/" + name + ".xnb";
            File.Copy(builtPath, movePath, true);

            ContentManager newShaderContent = new ContentManager(shaderContent.ServiceProvider, shaderContent.RootDirectory);
            var newShaders = new Dictionary<string, Effect>();
            // Unfortunately due to the way monogame works we need to reload all the shaders into a new content manager
            foreach (var shaderName in state.shaders.Keys)
            {
                var effect = newShaderContent.Load<Effect>(shaderName);
                newShaders.Add(shaderName.ToLower(), effect);
            }

            // Shut down the old content manager and swap
            shaderContent.Unload();
            shaderContent.Dispose();
            shaderContent = newShaderContent;
            state.shaders = newShaders;

        }
#endif

        public Effect GetShader(string name)
        {
            return state.shaders[name.ToLower()];
        }

        public void AddShader(string name)
        {
            if (!state.shaders.ContainsKey(name.ToLower()))
            {
                var shader = content.Load<Effect>(name.ToLower());
                state.shaders.Add(name.ToLower(), shader);
            }
        }




    }



}
