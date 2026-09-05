"""Pure policy/source checks for staged celestial atmosphere composition.

These checks do not compile Unity shaders and do not replace Game-view QA. They
pin the altitude reveal boundary and phase-opacity behavior so integration does
not regress into a surface-visible system planet or an opaque new-moon disc.
"""

from __future__ import annotations

import json
import math
from pathlib import Path


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def smoothstep01(value: float) -> float:
    value = clamp01(value)
    return value * value * (3.0 - 2.0 * value)


def system_planet_visibility(
    camera_radius: float,
    inner_radius: float,
    outer_multiplier: float,
    minimum_atmosphere_height: float,
) -> float:
    effective_multiplier = max(
        outer_multiplier, 1.0 + minimum_atmosphere_height / inner_radius
    )
    outer_radius = inner_radius * effective_multiplier
    reveal_distance = max(0.25, (outer_radius - inner_radius) * 0.12)
    linear = (camera_radius - outer_radius) / reveal_distance
    return smoothstep01(linear)


def shader_smoothstep(edge0: float, edge1: float, value: float) -> float:
    return smoothstep01((value - edge0) / (edge1 - edge0))


def moon_center_alpha(phase: float) -> float:
    # At the apparent disc center, dot(normal, sun) = 2*phase-1 for the
    # ephemeris convention. The shader's crescent boundary is spatial; this
    # scalar check only pins new/full center behavior.
    sun_alignment = 2.0 * clamp01(phase) - 1.0
    diffuse = shader_smoothstep(-0.025, 0.16, sun_alignment)
    earthshine = 0.025 * 0.30
    illumination = earthshine + diffuse * 1.25
    return clamp01(illumination * 1.08)


def main() -> None:
    folder = Path(__file__).resolve().parent
    behaviour = (folder / "CelestialSystemBehaviour.cs").read_text(encoding="utf-8")
    shader = (folder / "ScaledCelestialBody.shader").read_text(encoding="utf-8")

    radius = 55.1
    multiplier = 1.055
    minimum_height = 8.0
    effective_multiplier = max(multiplier, 1.0 + minimum_height / radius)
    outer = radius * effective_multiplier
    reveal = max(0.25, (outer - radius) * 0.12)
    altitude_samples = {
        "baseSurface": system_planet_visibility(radius, radius, multiplier, minimum_height),
        "normalArenaCamera": system_planet_visibility(61.03511, radius, multiplier, minimum_height),
        "insideAtmosphere": system_planet_visibility(outer - 0.01, radius, multiplier, minimum_height),
        "outerBoundary": system_planet_visibility(outer, radius, multiplier, minimum_height),
        "halfReveal": system_planet_visibility(outer + reveal * 0.5, radius, multiplier, minimum_height),
        "outsideReveal": system_planet_visibility(outer + reveal, radius, multiplier, minimum_height),
    }
    phase_samples = {
        "newCenter": moon_center_alpha(0.0),
        "quarterCenter": moon_center_alpha(0.5),
        "fullCenter": moon_center_alpha(1.0),
    }

    assert altitude_samples["baseSurface"] == 0.0
    assert altitude_samples["normalArenaCamera"] == 0.0
    assert altitude_samples["insideAtmosphere"] == 0.0
    assert altitude_samples["outerBoundary"] == 0.0
    assert math.isclose(altitude_samples["halfReveal"], 0.5, abs_tol=1.0e-9)
    assert altitude_samples["outsideReveal"] == 1.0
    assert phase_samples["newCenter"] < 0.01
    assert phase_samples["newCenter"] < phase_samples["quarterCenter"] < phase_samples["fullCenter"]
    assert phase_samples["fullCenter"] == 1.0

    source_contracts = {
        "transparentBeforeAtmosphere": '"Queue"="Transparent-100"' in shader,
        "premultipliedBlend": "Blend One OneMinusSrcAlpha" in shader,
        "noCelestialDepthWrite": "ZWrite Off" in shader,
        "phaseDrivenBySun": "dot(normal, sun)" in shader,
        "moonNotAltitudeHidden": (
            "ApplyCelestialProperties(moon, ref _moonProperties, 1f, true" in behaviour
        ),
        "systemPlanetUsesOuterRadius": (
            "atmosphereProfile.EffectiveOuterRadiusMultiplier(innerRadius)" in behaviour
            and "Mathf.InverseLerp(outerRadius, outerRadius + revealDistance, cameraRadius)" in behaviour
        ),
        "atmosphereIncludesPlayableEnvelope": (
            "minimumAtmosphereHeightMeters = 8f" in
            (folder / "AtmosphereProfile.cs").read_text(encoding="utf-8")
            and "EffectiveOuterRadiusMultiplier(radius)" in behaviour
        ),
    }
    assert all(source_contracts.values()), source_contracts

    report = {
        "result": "Passed",
        "scope": "Pure altitude/phase policy and staged-source contracts; Unity compile and visual QA remain required.",
        "altitudePolicy": {
            "innerRadius": radius,
            "outerMultiplier": multiplier,
            "minimumAtmosphereHeightMeters": minimum_height,
            "effectiveOuterMultiplier": effective_multiplier,
            "outerRadius": outer,
            "revealDistance": reveal,
            "samples": altitude_samples,
        },
        "moonCenterPhasePolicy": phase_samples,
        "sourceContracts": source_contracts,
    }
    output = folder / "CelestialCompositionProof.json"
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(output.name)


if __name__ == "__main__":
    main()
