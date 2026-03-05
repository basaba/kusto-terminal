import { useState, useCallback } from "react";
import type { PaneId } from "../core/models.js";

const PANE_ORDER: PaneId[] = ["connections", "query", "results"];

export function useFocusManager(initial: PaneId = "connections") {
  const [activePane, setActivePane] = useState<PaneId>(initial);

  const moveLeft = useCallback(() => {
    setActivePane((current) => {
      const idx = PANE_ORDER.indexOf(current);
      return PANE_ORDER[Math.max(0, idx - 1)] ?? current;
    });
  }, []);

  const moveRight = useCallback(() => {
    setActivePane((current) => {
      const idx = PANE_ORDER.indexOf(current);
      return PANE_ORDER[Math.min(PANE_ORDER.length - 1, idx + 1)] ?? current;
    });
  }, []);

  const moveUp = useCallback(() => {
    setActivePane((current) => {
      if (current === "results") return "query";
      return current;
    });
  }, []);

  const moveDown = useCallback(() => {
    setActivePane((current) => {
      if (current === "query") return "results";
      return current;
    });
  }, []);

  return {
    activePane,
    setActivePane,
    moveLeft,
    moveRight,
    moveUp,
    moveDown,
  };
}
