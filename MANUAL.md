The following is not set in stone and may change as development continues.

# Introduction

[DevConsole] is a Lua-based Unity console framework that allows for the dynamic
prodding, manipulation, and scripting of Unity objects from within a given game. 

[DevConsole] uses the Lua programming language for its scripting engine, as such
if you are unfamiliar with its syntax or how it works it might be useful to read
a bit about how it works, for example, in Lua arrays start at 1 instead of 0,
and this convention is kept the same with the API exposed by [DevConsole].

# Scripting

[DevConsole] is capable of handling long-running scripts or loops. Each submitted input is
considered as seperate and individual script object - so at least for as of now,
complicated or multi-step scripts must be inputted as a single line, meaning it may
be helpful to write your desired script in an external file before copying it to the console.
Unlike Python, Lua does not consider whitespace meaningful.

# Information about an object

Once you have grabbed an object(s)/component(s)/etc as a variable within the Lua context,
you can run print(var) and it will print a general summary of that object/component, or the objects/components
within an array if you are calling print() on one.

# Arrays

If "arr" is an array returned by the API, `item = arr.Get(1)` will assign the first item in arr to item.
Arrays start at 1 to match Lua.

# API/Commands

## Misc

wait(int ms)

- Pauses the execution of a script for (approximately) the given amount of miliseconds. 
Intended for controlling the execution speed of long-running scripts.

## History
 
dumpcmdhist() 

- Places the history of all submitted commands into the clipboard

dumploghist()

- Places the entire console log history into the clipboard

## Objects

### Grabbing objects

findfirst(string objectName)

- Returns the first found object with the given name

findall(string objectName)

- Returns an array of all found objects with the given name

findfirstbytag(string tagName)

- Returns the first found object with the given tag

findallbytag("ObjectName")

- Returns an array of all found objects with the given tag

### Manipulating Objects

gameobject.SetPos(float x, float y, float z)

- Sets the position of the object to x, y, z

gameobject.AddPos(float x, float y, float z)

- Adds x, y, z to the object's position

gameobject.SetScale(float x, float y, float z)

- Sets the object's scale to x, y, z

gameobject.AddScale(float x, float y, float z)

- Adds x, y, z to the object's scale

gameobject.SetRot(float x, float y, float z)

- Sets the rotation of the object to x, y, z

gameobject.AddRot(float x, float y, float z)

- Adds x, y, z to the object's rotation

gameobject.SetActive(true/false)

- Toggles whether the object is active

### Getting Object Components

gameobject.GetAllComponents()

- Returns an array of all components attached to the object 

gameobject.GetComponent(string componentName)

- Returns the component with the given name, if it exists

### Parent/Child/Siblings

gameobject.GetParent()

- Returns the parent of the object

gameobject.GetChildren()

- Returns an array of the children of the object

gameobject.GetChild(int index)

- Returns the child of the object at the given index

gameobject.GetSiblings(bool includeSelf)

- Returns the children of the parent object that the gameobject itself is a child of.
By default, includeSelf is false, excluding the gameobject you are requesting the siblings of.
(meaning that unless you want it included you do not have to include a bool in the call)

gameobject.GetSiblingIndex()

- Returns the index at which the gameobject is at in the children of the gameobject's parent

### Misc

gameobject.Delete()

- Deletes the gameobject

gameobject.Clone()

- Clones the gameobject and returns the copy

gameobject.GetTransform()

- Returns the transform object of the gameobject


## Components

component.ListMethods()

- Returns a string-array of the methods belonging to the component  

component.ListProperties()

- Returns a string-array of the properties belonging to the component

component.GetProperty(string propertyName)

- Returns the value of the given property on the component

component.SetProperty(string propertyName, value)

- Sets the property on the component to the given value

component.ListFields()

- Returns a string-array of the fields belonging to the component

component.GetField(string fieldName)

- Returns the value of the given field on the component

component.SetField(string fieldName, value)

- Sets the value on the component to the g iven value

component.InvokeMethod(string methodName, array arg[s]...)

- Calls the given method belonging to the component with the given arguments.
If the length of arguments is > 1, args must be an array/table.

# Scripts

scripts()

- Lists all currently running scripts and their IDs.

kill(int id)

- Kills the running script with the given ID.

killall()

- Kills all running scripts.