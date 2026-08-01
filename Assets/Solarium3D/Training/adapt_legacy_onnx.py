"""Adapt the legacy 187-observation/one-branch policy to the 3D tensor contract.

The original asset is never modified. The adapter preserves the observations the
legacy policy was trained on and emits a second, deterministic no-interaction
branch. A newly trained 194-observation LSTM should replace this compatibility
model once solarium-3d-v1 is evaluated and promoted.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
import onnx
from onnx import TensorProto, helper, numpy_helper


def _set_second_dimension(value_info: onnx.ValueInfoProto, size: int) -> None:
    dimensions = value_info.type.tensor_type.shape.dim
    dimensions[1].ClearField("dim_param")
    dimensions[1].dim_value = size


def adapt(source: Path, destination: Path) -> None:
    model = onnx.load(source)
    graph = model.graph

    inputs = {value.name: value for value in graph.input}
    outputs = {value.name: value for value in graph.output}
    if "obs_0" not in inputs or "action_masks" not in inputs:
        raise ValueError("Not an ML-Agents vector policy with obs_0/action_masks inputs")
    _set_second_dimension(inputs["obs_0"], 194)
    _set_second_dimension(inputs["action_masks"], 4)

    # Legacy internal signals were 0..7 and 9..19 in the current contract.
    # The 12x14 ray block moved from offset 19 to offset 26.
    indices = np.asarray(
        list(range(0, 8)) + list(range(9, 20)) + list(range(26, 194)),
        dtype=np.int64,
    )
    if indices.size != 187:
        raise AssertionError(indices.size)
    graph.initializer.append(numpy_helper.from_array(indices, "compat_observation_indices"))
    for node in graph.node:
        for index, name in enumerate(node.input):
            if name == "obs_0":
                node.input[index] = "compat_obs_legacy187"
    graph.node.insert(
        0,
        helper.make_node(
            "Gather",
            ["obs_0", "compat_observation_indices"],
            ["compat_obs_legacy187"],
            name="Compatibility_SelectLegacyObservations",
            axis=1,
        ),
    )

    for node in graph.node:
        for index, name in enumerate(node.output):
            if name == "discrete_actions":
                node.output[index] = "compat_legacy_discrete_actions"
            elif name == "deterministic_discrete_actions":
                node.output[index] = "compat_legacy_deterministic_actions"

    zero_value = helper.make_tensor("value", TensorProto.INT64, [1], [0])
    graph.node.extend(
        [
            helper.make_node(
                "Shape",
                ["compat_legacy_discrete_actions"],
                ["compat_action_shape"],
                name="Compatibility_ActionShape",
            ),
            helper.make_node(
                "ConstantOfShape",
                ["compat_action_shape"],
                ["compat_no_interaction"],
                name="Compatibility_NoInteraction",
                value=zero_value,
            ),
            helper.make_node(
                "Concat",
                ["compat_legacy_discrete_actions", "compat_no_interaction"],
                ["discrete_actions"],
                name="Compatibility_DiscreteActions",
                axis=1,
            ),
            helper.make_node(
                "Concat",
                ["compat_legacy_deterministic_actions", "compat_no_interaction"],
                ["deterministic_discrete_actions"],
                name="Compatibility_DeterministicDiscreteActions",
                axis=1,
            ),
        ]
    )

    for initializer in graph.initializer:
        if initializer.name == "discrete_act_size_vector":
            initializer.CopyFrom(
                numpy_helper.from_array(
                    np.asarray([[2.0, 2.0]], dtype=np.float32),
                    "discrete_act_size_vector",
                )
            )
            break
    _set_second_dimension(outputs["discrete_actions"], 2)
    _set_second_dimension(outputs["deterministic_discrete_actions"], 2)
    _set_second_dimension(outputs["discrete_action_output_shape"], 2)

    model.producer_name = "Solarium3D compatibility adapter"
    onnx.checker.check_model(model)
    destination.parent.mkdir(parents=True, exist_ok=True)
    onnx.save(model, destination)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    arguments = parser.parse_args()
    adapt(arguments.source.resolve(), arguments.destination.resolve())
    print(arguments.destination.resolve())


if __name__ == "__main__":
    main()
