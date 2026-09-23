import json
import pathlib

from jsonschema import Draft202012Validator
from referencing import Registry, Resource

schemas = pathlib.Path("schemas")
diagnostic = json.loads((schemas / "compiler-diagnostic.schema.json").read_text())
result = json.loads((schemas / "compiler-result.schema.json").read_text())
instance = json.loads(pathlib.Path("compiler-result.actual.json").read_text())

registry = Registry().with_resource(
    diagnostic["$id"],
    Resource.from_contents(diagnostic),
)
Draft202012Validator(result, registry=registry).validate(instance)
print("Compiler JSON Schema validation OK")
