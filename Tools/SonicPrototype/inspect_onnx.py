import json
import os
from collections import Counter
import onnx
from onnx import TensorProto, helper

path = r'Tools\SonicPrototype\Models\planner_sonic_6733128.onnx'
model = onnx.load_model(path, load_external_data=False)

def shape_of(value):
    tt = value.type.tensor_type
    dims=[]
    for dim in tt.shape.dim:
        if dim.HasField('dim_value'): dims.append(dim.dim_value)
        elif dim.HasField('dim_param'): dims.append(dim.dim_param)
        else: dims.append(None)
    return {'name':value.name,'dtype':TensorProto.DataType.Name(tt.elem_type),'shape':dims}

def json_value(value):
    if isinstance(value, bytes):
        return value.decode('utf-8')
    if isinstance(value, tuple):
        return [json_value(item) for item in value]
    if isinstance(value, list):
        return [json_value(item) for item in value]
    return value

def variants_of(operator):
    variants=[]
    for node in model.graph.node:
        if node.op_type != operator:
            continue
        attributes={item.name:json_value(helper.get_attribute_value(item)) for item in node.attribute}
        if attributes not in variants:
            variants.append(attributes)
    return variants

report={
 'path':path,
 'bytes':os.path.getsize(path),
 'irVersion':model.ir_version,
 'producerName':model.producer_name,
 'producerVersion':model.producer_version,
 'opsets':[{x.domain or 'ai.onnx':x.version} for x in model.opset_import],
 'inputs':[shape_of(x) for x in model.graph.input],
 'outputs':[shape_of(x) for x in model.graph.output],
 'nodes':len(model.graph.node),
 'initializers':len(model.graph.initializer),
 'operators':dict(sorted(Counter((n.domain or 'ai.onnx')+'::'+n.op_type for n in model.graph.node).items())),
 'operatorVariants':{
   operator:variants_of(operator)
   for operator in ['LayerNormalization','Resize','ScatterElements','ScatterND','Einsum','ArgMax','ArgMin','TopK','Pad','Mod']
 },
}
os.makedirs(r'Tools\SonicPrototype\Reports',exist_ok=True)
with open(r'Tools\SonicPrototype\Reports\OnnxGraphInspection.json','w',encoding='utf-8') as f: json.dump(report,f,indent=2)
print(json.dumps(report,indent=2))
