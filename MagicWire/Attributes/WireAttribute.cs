﻿namespace MagicWire;

/// <summary>
/// The wire attribute is used to mark classes, enums, fields and methods as wireable. When a member is wireable
/// it will be made available for wire operations on the frontend. The frontend TypeScript generator will generate
/// respective TypeScript code for the marked members.
/// 
/// <br/><br/>
/// There are a few special rules:<br/>
/// - <b>Class</b>: Classes must be marked as <b>partial</b>. In order to make fields and methods available, they also
/// must be marked with <see cref="WireAttribute"/>. Its mandatory to either call manually <see cref="WireContainer.ManageObject"/>
/// or to call the base constructor of the <see cref="WireableObject"/>.<br/>
/// - <b>Field</b>: If a field is marked as wireable, it will be transferrable. The source generators will generate
/// respective getter/setter properties for the field. They must be used to detect changes in the field value so the
/// synchronization can be triggered. The field can be private, at should be private.<br/>
/// - <b>Method</b>: By default all methods are unidirectional meaning they are meant to be called from the client,
/// executed on the server and the result is sent back to the client. MagicWire does not support bidirectional methods.
/// If a method's direction should be reversed (the server wants to send an update to the client), the method must be marked
/// with the <see cref="ToClientAttribute"/>. This will generate a special EventHandler in TypeScript which can be used to
/// process the event on the client-side. The method must be partial and can only be void. The body is automatically
/// generated by the Source Generator. Also, methods which are not marked with the <see cref="ToClientAttribute"/>
/// can have a parameter of type <see cref="IFrontend"/>. This parameter will be automatically set to the frontend which
/// calls the method. This can be used to identify the client which called the method.
///
/// <br/><br/>
/// MagicWire automatically decides which protocol should be used for any wire operation. Methods use HTTP by default,
/// while fields use SSE. This is because methods are usually used for one-time operations, while fields are
/// constant changes that should be synchronized.
///
/// <br/><br/>
/// <b>Important note:</b> Wireable classes/objects are singleton. Even if they are owend by a specific <see cref="IFrontend"/>
/// (by calling <see cref="IFrontend.Own(object)"/>), they are still singleton. This means that the same object will
/// only be created once on the client-side. Only DTOs and objects which are used to transfer data between the server
/// and the client can be created multiple times but their lifetime is limited to the current request.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Method)]
public sealed class WireAttribute : Attribute;