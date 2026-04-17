# Code Layout and File Explanations

## Root

### DeveloperConsole.cs

- Main object class exposed to and to be instantiated within Unity.

### ConsoleEngine.cs
   
- Engine that handles Lua interpretation and the running of scripts.

### LuaCommandAttribute.cs

- Attribute to allow dynamic adding and reading of functions to be exposed within the Lua context.

### Commands.cs

- C# functions that are exposed to be called within the Lua context.

### ConsoleDragHandle.cs / ConsoleResizeHandle.cs

- Files to handle parts of the console UI.

## ./Proxies/

### GameObjectProxy.cs

- Exposes the Unity idea of a Game Object to the Lua context with object-functions for interacting with and querying them.

### ComponentProxy.cs

- Like the above, for the Component objects.

### GameObjectProxyArray.cs/ComponentProxyArray.cs/StringArrayProxy.cs

- Wrappers for arrays of Game Objects, Components, and Strings in the Lua context.

### ReflectionProxyUtils.cs

- Code that enables invoking methods of components via reflection.