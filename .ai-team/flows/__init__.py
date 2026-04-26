from pathlib import Path


def default_flow_path() -> Path:
    return Path(__file__).resolve().parent / "software_delivery.yaml"


__all__ = ["default_flow_path"]
