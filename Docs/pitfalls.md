# Pitfalls

What fails silently, the fix, and the measurement that proves it. `L` likely, `P` possible, `U` unlikely. Ids are
append-only (see CLAUDE.md). Only measured findings go here.

**L1 — A ground inside the same ground is blank.** A `ThemeVariantScope` in a data template that asks for the
ground its window already has (a Paper card in the Paper library) shows no card colour: its `DynamicResource`s
resolve while the template is detached, find nothing, and are never looked up again, because the scope's variant
never changes. A scope on another ground is fine. Fix: `GroundScope` steps to the ground's base theme and back when
it is attached. Proof: in the library window, the Deep Work card's border had `Background` null while
`TryFindResource("Brush.Bg.Card")` on the same border returned White; one switch of the scope's variant made it
White. The library snapshot shows the Paper card white on the cream ground.
