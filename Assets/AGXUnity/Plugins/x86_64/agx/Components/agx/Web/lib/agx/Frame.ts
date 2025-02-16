/// <reference path="Math.ts"/>
/// <reference path="Util.ts"/>
/// <reference path="Type.ts"/>
/// <reference path="Component.ts"/>
/// <reference path="Value.ts"/>
/// <reference path="Buffer.ts"/>
/// <reference path="EntityStorage.ts"/>

module agx
{
  /**
  Data frame.
  */
  export class Frame extends Component
  {
    index : number;
    timeStamp : number;
    timeStep : number;
    isKeyFrame : boolean;

    constructor(index : number, timeStamp : number, timeStep : number)
    {
      super();
      this.index = index;
      this.timeStamp = timeStamp;
      this.timeStep = timeStep;
      this.isKeyFrame = false;
    }

    /**
    Construct a frame from a binary packet.
    */
    static CreateFromPacket(packet : ArrayBuffer) : Frame
    {
      var frameMessage = agx.StructuredMessage.ParseMessage(packet);
      return Frame.CreateFromMessage(frameMessage);
    }

    /**
    Construct a frame from a structured message.
    */
    static CreateFromMessage(message : agx.StructuredMessage)
    {
      var header = message.header;

      // console.log(JSON.stringify(message.header, undefined, 2));

      agx.AssertDefined(header.index);
      agx.AssertDefined(header.timeStamp);
      agx.AssertDefined(header.timeStep);
      agx.AssertDefined(header.keyFrame);

      var frame = new Frame(header.index, header.timeStamp, header.timeStep);
      frame.isKeyFrame = header.keyFrame;

      frame.addChildren(frame, header, message.binarySegment);

      return frame;
    }

    /**
    Merge another frame into this one. This will 'steal' buffers
    from the other frame which should be discarded after the merge.
    */
    merge(source : Frame)
    {
      agx.Assert(source.index > this.index);
      this.index = source.index;
      this.timeStep = source.timeStep;
      this.timeStamp = source.timeStamp;

      this.mergeChildren(this, source);
    }

    createRigidBodyBuffer(type : string, name : string)
    {
      var rbStorage = this.getObject("RigidBody");
      
      if (!rbStorage)
        return;

      var buffer = rbStorage.getObject(type);

      if (!buffer)
        return;

      var oldPositionBuffer = rbStorage.getObject(name);
      if (oldPositionBuffer)
        rbStorage.removeObject(oldPositionBuffer);
      
      var result;
      if (name == "position")
        result = Frame.extractPositionBuffer(buffer);
      else
        result = Frame.extractRotationBuffer(buffer);
      
      rbStorage.addObject(result);
    }


    static extractPositionBuffer(transformBuffer : agx.Buffer) : agx.Buffer
    {
      var positionBuffer = new agx.Buffer(agx.GetFormat("Vec3:64bit"), "position");
      positionBuffer.numElements = transformBuffer.numElements;
      positionBuffer.data = new Float64Array(positionBuffer.numElements * positionBuffer.format.numElements);

      for (var i = 0; i < positionBuffer.numElements; ++i)
      {
        var translateIndex = i * transformBuffer.format.numElements + 12;
        var posIndex = i * positionBuffer.format.numElements;

        for (var j = 0; j < 3; j++)
          positionBuffer.data[posIndex+j] = transformBuffer.data[translateIndex+j];
      }

      return positionBuffer;
    }


    static extractRotationBuffer(transformBuffer : agx.Buffer) : agx.Buffer
    {
      var rotationBuffer = new agx.Buffer(agx.GetFormat("Vec3:64bit"), "rotation");
      rotationBuffer.numElements = transformBuffer.numElements;
      rotationBuffer.data = new Float64Array(rotationBuffer.numElements * rotationBuffer.format.numElements);

      var i = 0;
      var j = 1;
      var k = 2;

      for (var index = 0; index < rotationBuffer.numElements; ++index)
      {
        var translateIndex = index * transformBuffer.format.numElements;
        var posIndex = index * rotationBuffer.format.numElements;
        
        var getMatrixCell = (buffer : agx.Buffer, index : any, x : any, y : any) =>
        {
          return buffer.data[index + x * 4 + y];
        };
        

        var cy = Math.sqrt(Math.pow( getMatrixCell(transformBuffer, translateIndex, j, k), 2) + Math.pow( getMatrixCell(transformBuffer, translateIndex, k, k), 2));
        if (cy > 16 * agx.Epsilon()){
          rotationBuffer.data[posIndex+0] = Math.atan2( getMatrixCell(transformBuffer, translateIndex, j, k), getMatrixCell(transformBuffer, translateIndex, k, k));
          rotationBuffer.data[posIndex+1] = Math.atan2(-getMatrixCell(transformBuffer, translateIndex, i, k), cy);
          rotationBuffer.data[posIndex+2] = Math.atan2( getMatrixCell(transformBuffer, translateIndex, i, j), getMatrixCell(transformBuffer, translateIndex, i, i));
        } else {
          rotationBuffer.data[posIndex+0] = Math.atan2(-getMatrixCell(transformBuffer, translateIndex, k, j), getMatrixCell(transformBuffer, translateIndex, j, j));
          rotationBuffer.data[posIndex+1] = Math.atan2(-getMatrixCell(transformBuffer, translateIndex, i, k), cy);
          rotationBuffer.data[posIndex+2] = 0;
        }
      }

      return rotationBuffer;
    }

    static extractMagnitudeBuffer(buffer : agx.Buffer) : agx.Buffer
    {
      var subComponentBuffer = new agx.Buffer(agx.GetFormat("Real:64bit"), buffer.name);
      subComponentBuffer.numElements = buffer.numElements;
      subComponentBuffer.data = new Float64Array(buffer.numElements);

      for (var i = 0; i < buffer.numElements; ++i)
        subComponentBuffer.data[i] = Math.sqrt(
                                      Math.pow(buffer.data[i * buffer.format.numElements + 0], 2) +
                                      Math.pow(buffer.data[i * buffer.format.numElements + 1], 2) +
                                      Math.pow(buffer.data[i * buffer.format.numElements + 2], 2));

      return subComponentBuffer;
    }

    static extractSubComponentBuffer(buffer : agx.Buffer, subComponentIndex : number) : agx.Buffer
    {
      var subComponentBuffer = new agx.Buffer(agx.GetFormat("Real:64bit"), buffer.name);
      subComponentBuffer.numElements = buffer.numElements;
      subComponentBuffer.data = new Float64Array(buffer.numElements);

      for (var i = 0; i < buffer.numElements; ++i)
        subComponentBuffer.data[i] = buffer.data[i * buffer.format.numElements + subComponentIndex];

      return subComponentBuffer;
    }

    ///////////////////////////////////////////////////////////////////

    private addChildren(parent : Component, frameNode : any, binarySegment : Uint8Array)
    {
      if (!agx.IsDefined(frameNode.children))
        return;

      for (var i = 0; i < frameNode.children.length; ++i)
      {
        var child = frameNode.children[i];
        agx.AssertDefined(child.nodeType);
        agx.AssertDefined(child.name);

        if (child.nodeType == "Storage")
        {
          this.addStorage(parent, child, binarySegment);
        }
        else if (child.nodeType == "Buffer")
        {
          this.addBuffer(parent, child, binarySegment);
        }
        else if (child.nodeType == "Value")
        {
          this.addValue(parent, child, binarySegment);
        }
        else if (child.nodeType == "Component")
        {
          this.addComponent(parent, child, binarySegment);
        }
        else
        {
          throw "Unknown node type " + child.nodeType;
        }
      }
    }

    private addStorage(parent : Component, storageNode : any, binarySegment : Uint8Array)
    {
      agx.AssertDefined(storageNode.entity);
      agx.AssertDefined(storageNode.numElements);
      agx.AssertDefined(storageNode.capacity);

      var storage = new agx.EntityStorage(storageNode.entity);
      storage.name = storageNode.name;
      storage.numElements = storageNode.numElements;
      storage.capacity = storageNode.capacity;
      
      parent.addObject(storage);

      this.addChildren(storage, storageNode, binarySegment);
    }

    private addBuffer(parent : Component, bufferNode : any, binarySegment : Uint8Array)
    {
      parent.addObject(agx.ExtractBuffer(bufferNode, binarySegment));
    }

    private addValue(parent : Component, valueNode : any, binarySegment : Uint8Array)
    {
      parent.addObject(agx.ExtractValue(valueNode, binarySegment));
    }

    private addComponent(parent : Component, componentNode : any, binarySegment : Uint8Array)
    {
      var component = new agx.Component(componentNode.name);
      parent.addObject(component);
      this.addChildren(component, componentNode, binarySegment);
    }

    private mergeChildren(parent : Component, otherParent : Component)
    {
      for (var childName in otherParent.objects)
      {
        var child = otherParent.getObject(childName);
        var existingChild = parent.getObject(childName);

        otherParent.removeObject(child);

        if (!existingChild)
        {
          parent.addObject(child);
        }
        else
        {
          if (child instanceof agx.EntityStorage)
          {
            agx.Assert(existingChild instanceof agx.EntityStorage);
            this.mergeStorage(existingChild, child);
          }
          else if (child instanceof agx.Buffer)
          {
            agx.Assert(existingChild instanceof agx.Buffer);
            parent.removeObject(existingChild);
            parent.addObject(child);
            // this.mergeBuffer(existingChild, child);
          }
          else if (child instanceof agx.Value)
          {
            agx.Assert(existingChild instanceof agx.Value);
            parent.removeObject(existingChild);
            parent.addObject(child);
          }
          else if (child instanceof agx.Component)
          {
            agx.Assert(existingChild instanceof agx.Component);
            this.mergeChildren(existingChild, child);
          }
          else
          {
            agx.Abort(child.getPath() + ": Unknown type")
          }
        }

      }
    }

    private mergeStorage(target : agx.EntityStorage, source : agx.EntityStorage)
    {
      agx.Assert(target.entity == source.entity);

      target.numElements = source.numElements;
      target.capacity = source.capacity;
      this.mergeChildren(target, source);
    }

    private mergeBuffer(target : agx.Buffer, source : agx.Buffer)
    {
    }
  }

  /**
  PreHeader is meta data for all messages sent over socket.
  */
  export class PreHeader 
  {
    static NumElements = 6;
    static NumBytes = PreHeader.NumElements * Uint32Array.BYTES_PER_ELEMENT;

    id : number;
    idResponse : number;
    uriSize : number;
    headerSize : number;
    binarySegmentOffset : number;
    binarySegmentSize : number;

    constructor(id : number = 0, idResponse : number = 0, uriSize : number = 0, headerSize : number = 0, binarySegmentOffset : number = 0, binarySegmentSize : number = 0)
    {
      this.id = id;
      this.idResponse = idResponse;
      this.uriSize = uriSize;
      this.headerSize = headerSize;
      this.binarySegmentOffset = binarySegmentOffset;
      this.binarySegmentSize = binarySegmentSize;
    }

    /**
    Construct a preheader from a binary packet.
    */
    static CreateFromPacket(packet : ArrayBuffer) : PreHeader
    {
      var preHeaderData = new Uint32Array( packet, 0, PreHeader.NumElements );
      return new PreHeader(preHeaderData[0], preHeaderData[1], preHeaderData[2], preHeaderData[3], preHeaderData[4], preHeaderData[5]);
    }

    /**
    \return The full size of the represented packet.
    */
    getPacketSize() : number
    {
      return this.binarySegmentOffset > 0 ? this.binarySegmentOffset + this.binarySegmentSize : PreHeader.NumBytes + this.uriSize + this.headerSize;
    }

    /**
    Write the preheader to a packet.
    */
    writeToPacket(packet : ArrayBuffer)
    {
      if (packet.byteLength < PreHeader.NumBytes)
        throw "Invalid packet size: " + packet.byteLength;

      var data = new Uint32Array( packet, 0, PreHeader.NumElements );
      data[0] = this.id;
      data[1] = this.idResponse;
      data[2] = this.uriSize;
      data[3] = this.headerSize;
      data[4] = this.binarySegmentOffset;
      data[5] = this.binarySegmentSize;
    }
  }

  /**
  A serialized message for communication over socket.
  */
  export class StructuredMessage
  {
    static BinarySegmentAlignment = 32;

    preHeader : PreHeader;
    uri : string;
    header : any;
    headerString : string;
    binarySegment : Uint8Array;

    packet : ArrayBuffer; // Full packet: preheader + header + binary segment


    setMessageId( id : number )
    {
      this.preHeader.id = id;
      this.preHeader.writeToPacket( this.packet );
    }


    /**
    Build a structued message from a header + (optional) binary segment
    */
    static BuildMessage(uri : string, header? : any, binarySegments? : ArrayBufferView[]) : StructuredMessage
    {
      var message = new StructuredMessage();
      message.uri = uri;
      message.header = agx.IsDefined(header) ? header : null;
      message.headerString = JSON.stringify(message.header);

      // Calculate pre header
      message.preHeader = new PreHeader();
      var preHeader = message.preHeader;
      preHeader.uriSize = message.uri.length;
      preHeader.headerSize = message.headerString.length; // Note, this will fail for non-ASCII characters.

      if (binarySegments != undefined)
      {
        for (var i = 0; i < binarySegments.length; ++i)
          preHeader.binarySegmentSize += binarySegments[i].byteLength;
      }

      var headerEnd = PreHeader.NumBytes + preHeader.uriSize + preHeader.headerSize;
      preHeader.binarySegmentOffset = agx.AlignCeil( headerEnd, StructuredMessage.BinarySegmentAlignment );

      // Create packet
      message.packet = new ArrayBuffer( preHeader.getPacketSize() );

      // Write pre header
      preHeader.writeToPacket(message.packet);

      // Write URI
      var uriView = new Uint8Array(message.packet, PreHeader.NumBytes, preHeader.uriSize);
      agx.WriteStringToArray(uriView, message.uri);

      // Write header
      var headerView = new Uint8Array(message.packet, PreHeader.NumBytes + preHeader.uriSize, preHeader.headerSize);
      agx.WriteStringToArray(headerView, message.headerString);

      // Write binary segment
      if (binarySegments != undefined)
      {
        message.binarySegment = new Uint8Array(message.packet, preHeader.binarySegmentOffset, preHeader.binarySegmentSize);
        var currentIndex = 0;
        for (var i = 0; i < binarySegments.length; ++i)
        {
          var segment = binarySegments[i];
          var byteArray = new Uint8Array(segment.buffer, segment.byteOffset, segment.byteLength);
          for (var j = 0; j < byteArray.length; ++j)
            message.binarySegment[currentIndex++] = byteArray[j];
        }

        agx.Assert(currentIndex == preHeader.binarySegmentSize);
      }


      return message;
    }

    /**
    Parse a structured message from a received binary packet
    */
    static ParseMessage(packet : ArrayBuffer) : StructuredMessage
    {
      var message = new StructuredMessage();
      message.packet = packet;
      message.preHeader = PreHeader.CreateFromPacket(packet);
      var preHeader = message.preHeader;

      if (preHeader.getPacketSize() != packet.byteLength)
        throw "Invalid packet: PreHeader says it should be " + preHeader.getPacketSize() + " bytes, but actual data is " + packet.byteLength + " bytes";

      var uriView = new Uint8Array(packet, PreHeader.NumBytes, preHeader.uriSize);
      message.uri = agx.ExtractStringFromArray(uriView);
      var headerView = new Uint8Array(packet, PreHeader.NumBytes + preHeader.uriSize, preHeader.headerSize);
      message.headerString = agx.ExtractStringFromArray(headerView);
      message.header = JSON.parse(message.headerString);
      message.binarySegment = new Uint8Array(packet, preHeader.binarySegmentOffset, preHeader.binarySegmentSize);
      // console.log("Parsed message " + message.uri + " with " + preHeader.headerSize + " character header and " + preHeader.binarySegmentSize + " bytes binary segment");

      return message;
    }

    /**
    Extract a buffer from the message.
    */
    extractBuffer(bufferHeader : any) : Buffer
    {
      return agx.ExtractBuffer(bufferHeader, this.binarySegment);
    }
  }


  export class StructuredMessagePromise
  {
    private onSuccessCallback : (message : StructuredMessage) => any;
    private message : StructuredMessage;

    constructor( message : StructuredMessage )
    {
      this.message = message;
    }

    success( callback : (message : agx.StructuredMessage) => any )
    {
      this.onSuccessCallback = callback;
    }

    signalSuccess()
    {
      if ( agx.IsDefined(this.onSuccessCallback) )
        this.onSuccessCallback( this.message );
    }
  }

}
