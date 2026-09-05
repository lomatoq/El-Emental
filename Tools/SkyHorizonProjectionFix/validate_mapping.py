"""Pure numerical proof for the staged seam-free direction cloud mapping."""

from __future__ import annotations

import json
import math
from pathlib import Path


def normalize(value: tuple[float, float, float]) -> tuple[float, float, float]:
    length = math.sqrt(sum(component * component for component in value))
    return tuple(component / length for component in value)


def direction(azimuth: float, elevation: float) -> tuple[float, float, float]:
    horizontal = math.cos(elevation)
    return (
        horizontal * math.cos(azimuth),
        math.sin(elevation),
        horizontal * math.sin(azimuth),
    )


def old_projective_uv(ray: tuple[float, float, float]) -> tuple[float, float]:
    x, y, z = ray
    denominator = max(0.30, abs(y) + 0.22)
    return x / denominator * 1.08, z / denominator * 1.08


def latlong_uv(ray: tuple[float, float, float]) -> tuple[float, float]:
    x, y, z = normalize(ray)
    return (
        math.atan2(z, x) / (2.0 * math.pi) + 0.5,
        math.asin(max(-1.0, min(1.0, y))) / math.pi + 0.5,
    )


def periodic_texture(uv: tuple[float, float]) -> float:
    """Continuous repeatable stand-in for the authored CloudNoise64 texture."""
    u, v = uv
    return 0.5 + 0.18 * math.sin(2.0 * math.pi * u + 0.31) + \
        0.16 * math.sin(2.0 * math.pi * v - 0.77) + \
        0.08 * math.sin(2.0 * math.pi * (u + v) + 1.13)


def triplanar_noise(ray: tuple[float, float, float]) -> tuple[float, tuple[float, float, float]]:
    x, y, z = normalize(ray)
    raw = (abs(x) ** 4, abs(y) ** 4, abs(z) ** 4)
    total = sum(raw)
    weights = tuple(value / total for value in raw)
    uv_x = (z * 0.5 + 0.5 + 0.173, y * 0.5 + 0.5 + 0.619)
    uv_y = (x * 0.5 + 0.5 + 0.487, z * 0.5 + 0.5 + 0.271)
    uv_z = (x * 0.5 + 0.5 + 0.731, y * 0.5 + 0.5 + 0.043)
    value = sum(weight * periodic_texture(uv) for weight, uv in zip(weights, (uv_x, uv_y, uv_z)))
    return value, weights


def distance(left: tuple[float, float], right: tuple[float, float]) -> float:
    return math.hypot(left[0] - right[0], left[1] - right[1])


def angular_differentials(mapping, elevation_degrees: float) -> tuple[float, float]:
    step = math.radians(0.25)
    elevation = math.radians(elevation_degrees)
    center = mapping(direction(0.0, elevation))
    along_horizon = distance(center, mapping(direction(step, elevation)))
    across_horizon = distance(center, mapping(direction(0.0, elevation + step)))
    return along_horizon, across_horizon


def main() -> None:
    old_along, old_across = angular_differentials(old_projective_uv, 0.0)
    old_aspect = old_along / max(1.0e-12, old_across)

    epsilon = 1.0e-4
    pole_rays = {
        "+x": normalize((epsilon, 1.0, 0.0)),
        "-x": normalize((-epsilon, 1.0, 0.0)),
        "+z": normalize((0.0, 1.0, epsilon)),
        "-z": normalize((0.0, 1.0, -epsilon)),
    }
    pole_samples = {}
    for name, ray in pole_rays.items():
        noise, weights = triplanar_noise(ray)
        pole_samples[name] = {
            "latLongUv": latlong_uv(ray),
            "triplanarNoise": noise,
            "triplanarWeights": weights,
        }

    latlong_pole_span = max(sample["latLongUv"][0] for sample in pole_samples.values()) - \
        min(sample["latLongUv"][0] for sample in pole_samples.values())
    triplanar_pole_span = max(sample["triplanarNoise"] for sample in pole_samples.values()) - \
        min(sample["triplanarNoise"] for sample in pole_samples.values())

    directions = [
        normalize((math.sin(i * 0.37), math.cos(i * 0.61), math.sin(i * 0.83 + 0.2)))
        for i in range(1, 129)
    ]
    weight_errors = []
    minimum_dominant_weight = 1.0
    for ray in directions:
        _, weights = triplanar_noise(ray)
        weight_errors.append(abs(sum(weights) - 1.0))
        minimum_dominant_weight = min(minimum_dominant_weight, max(weights))

    assert old_aspect > 100.0, old_aspect
    assert latlong_pole_span > 0.7, latlong_pole_span
    assert triplanar_pole_span < 0.001, triplanar_pole_span
    assert max(weight_errors) < 1.0e-12, max(weight_errors)
    assert minimum_dominant_weight >= (1.0 / 3.0), minimum_dominant_weight

    report = {
        "result": "Passed",
        "contract": (
            "The old clamped plane projection collapses vertical variation at the horizon. "
            "Latitude/longitude removes that stretch but has a pole singularity. The final "
            "soft cube-style projection has normalized bounded weights and remains continuous "
            "through the former pole while preserving finite two-dimensional texture support."
        ),
        "oldHorizonAngularAspect": old_aspect,
        "latLongPoleUSpanForNearIdenticalDirections": latlong_pole_span,
        "triplanarPoleNoiseSpanForNearIdenticalDirections": triplanar_pole_span,
        "maximumWeightSumError": max(weight_errors),
        "minimumDominantPlaneWeight": minimum_dominant_weight,
        "poleSamples": pole_samples,
    }
    output = Path(__file__).with_name("SeamFreeDirectionCloudMappingProof.json")
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(output.name)


if __name__ == "__main__":
    main()
